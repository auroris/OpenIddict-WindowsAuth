using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using System;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using static OpenIddict.Abstractions.OpenIddictConstants;
using static OpenIddict.Server.OpenIddictServerEvents;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Server.IISIntegration;
using ActiveDirectory;

namespace IdentityServer
{
    /// <summary>
    /// Configures the OpenIddict server and validation stack.
    /// Runs in degraded mode (no user store) with custom event handlers that resolve
    /// identity claims from Windows Authentication and Active Directory.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Supported flows: Authorization Code and Implicit.
    /// Supported scopes: <c>openid</c>, <c>email</c>, <c>profile</c>, <c>roles</c>.
    /// </para>
    /// <para>
    /// Encryption and signing keys are ephemeral — they do not survive application restarts.
    /// This is acceptable for environments where the IIS application pool is long-lived, but
    /// means that tokens issued before a restart cannot be validated afterwards.
    /// </para>
    /// </remarks>
    public class IdentityServer
    {
        private sealed class ClientConfig
        {
            public string ClientId { get; init; } = "";
            public string? ClientSecret { get; init; }
        }

        /// <summary>
        /// Finds a matching client from <c>IdentityServer:Clients</c> by exact <paramref name="clientId"/>
        /// or by the <c>*</c> wildcard entry. Returns <see langword="null"/> if the list is configured
        /// but contains no match, or a wildcard <c>ClientConfig</c> if the list is absent (open access).
        /// </summary>
        private static ClientConfig? FindClient(string? clientId)
        {
            var clients = Program.Configuration.GetSection("IdentityServer:Clients").Get<ClientConfig[]>();
            if (clients == null || clients.Length == 0)
                return new ClientConfig { ClientId = "*" }; // no list configured: accept any client

            return clients.FirstOrDefault(c => c.ClientId.Equals(clientId ?? "", StringComparison.OrdinalIgnoreCase))
                ?? clients.FirstOrDefault(c => c.ClientId == "*");
        }

        /// <summary>
        /// Returns a logger for this class, resolved from the request's DI container when available,
        /// falling back to <see cref="Program.LoggerFactory"/> or a null logger.
        /// </summary>
        private static ILogger GetLogger(HttpContext? httpContext) =>
            (httpContext?.RequestServices.GetService<ILoggerFactory>()
             ?? Program.LoggerFactory
             ?? NullLoggerFactory.Instance)
            .CreateLogger<IdentityServer>();

        private static Regex[]? _validGroupPatterns;
        private static Regex[] ValidGroupPatterns => _validGroupPatterns ??=
            Program.Configuration.GetSection("IdentityServer:Groups").Get<string[]>()!
                .Select(g => new Regex(g, RegexOptions.IgnoreCase | RegexOptions.Compiled))
                .ToArray();

        /// <summary>
        /// Registers OpenIddict server and validation services in the DI container.
        /// Reads <c>IdentityServer:ServerUri</c>, <c>IdentityServer:Hosts</c>, and
        /// <c>IdentityServer:Groups</c> from <see cref="Program.Configuration"/>.
        /// </summary>
        /// <param name="services">The application service collection.</param>
        public static void Add(IServiceCollection services)
        {
            services.AddOpenIddict().AddServer(options =>
            {
                // Ephemeral keys: suitable for IIS-hosted scenarios where the app pool is long-lived
                options.AddEphemeralEncryptionKey().AddEphemeralSigningKey();
                options.AllowAuthorizationCodeFlow();
                options.AllowImplicitFlow();
                var serverUri = Program.Configuration.GetSection("IdentityServer:ServerUri").Get<string>();
                if (!string.IsNullOrEmpty(serverUri) && serverUri != "*")
                    options.SetIssuer(new Uri(serverUri));
                options.SetAuthorizationEndpointUris("connect/authorize")
                       .SetTokenEndpointUris("connect/token");
                options.EnableDegradedMode();
                options.UseAspNetCore()
                    .DisableTransportSecurityRequirement();
                options.RegisterScopes(Scopes.OpenId, Scopes.Email, Scopes.Profile, Scopes.Roles);

                // Validate authorization requests: verify client_id against IdentityServer:Clients
                // and redirect_uri host against IdentityServer:Hosts.
                options.AddEventHandler<ValidateAuthorizationRequestContext>(builder =>
                    builder.UseInlineHandler(context =>
                    {
                        var logger = GetLogger(context.Transaction.GetHttpRequest()?.HttpContext);

                        if (FindClient(context.Request.ClientId) == null)
                        {
                            logger.LogWarning("Authorization request rejected: unknown client_id '{ClientId}'",
                                context.Request.ClientId);
                            context.Reject(
                                error: Errors.InvalidClient,
                                description: "The specified client_id is not valid.");
                            return default;
                        }

                        var redirectHost = new Uri(context.RedirectUri!).Host;
                        if (Program.Configuration.GetSection("IdentityServer:Hosts").Get<string[]>()!
                            .Any(s => new Uri(s).Host.Equals(redirectHost, StringComparison.OrdinalIgnoreCase)))
                        {
                            return default;
                        }

                        logger.LogWarning("Authorization request rejected: redirect_uri '{RedirectUri}' host not in allowed list",
                            context.RedirectUri);
                        context.Reject(
                            error: Errors.InvalidClient,
                            description: "The specified redirect_uri is not valid.");
                        return default;
                    }));

                // Validate token requests: verify client_id against IdentityServer:Clients, and
                // client_secret if one is configured for the matched client.
                // Use ClientSecret: "*" to accept any secret without validating it.
                options.AddEventHandler<ValidateTokenRequestContext>(builder =>
                    builder.UseInlineHandler(context =>
                    {
                        var logger = GetLogger(context.Transaction.GetHttpRequest()?.HttpContext);

                        var client = FindClient(context.Request.ClientId);
                        if (client == null)
                        {
                            logger.LogWarning("Token request rejected: unknown client_id '{ClientId}'",
                                context.Request.ClientId);
                            context.Reject(
                                error: Errors.InvalidClient,
                                description: "The specified client_id is not valid.");
                            return default;
                        }

                        if (client.ClientSecret != null && client.ClientSecret != "*" &&
                            !string.Equals(client.ClientSecret, context.Request.ClientSecret, StringComparison.Ordinal))
                        {
                            logger.LogWarning("Token request rejected: invalid client_secret for client '{ClientId}'",
                                context.Request.ClientId);
                            context.Reject(
                                error: Errors.InvalidClient,
                                description: "The specified client_secret is not valid.");
                            return default;
                        }

                        return default;
                    }));

                // Handle authorization requests: build claims from Windows identity and AD
                options.AddEventHandler<HandleAuthorizationRequestContext>(builder =>
                    builder.UseInlineHandler(async context =>
                    {
                        var logger = GetLogger(context.Transaction.GetHttpRequest()?.HttpContext);
                        string? winAccountName = null;
                        try
                        {
                            HttpRequest request = context.Transaction.GetHttpRequest()
                                ?? throw new InvalidOperationException("The ASP.NET Core request cannot be retrieved.");

                            AuthenticateResult result = await request.HttpContext.AuthenticateAsync(IISDefaults.AuthenticationScheme);
                            if (!result.Succeeded)
                            {
                                logger.LogWarning("Windows authentication failed for authorization request from {RemoteIp}: {Failure}",
                                    request.HttpContext.Connection.RemoteIpAddress,
                                    result.Failure?.Message ?? "(no details)");
                                context.Reject(error: Errors.AccessDenied, description: "Windows authentication failed.");
                                return;
                            }

                            ClaimsIdentity identity = new ClaimsIdentity(TokenValidationParameters.DefaultAuthenticationType);
                            WindowsIdentity wi = (WindowsIdentity)request.HttpContext.User.Identity!;

                            // S-1-2-0 is the "Local" well-known SID; its presence means the user
                            // is logged on locally and Active Directory may not be reachable
                            bool isLocal = wi.FindAll(ClaimTypes.GroupSid).Any(g => g.Value == "S-1-2-0");

                            winAccountName = wi.FindFirst(ClaimTypes.Name)!.Value;
                            string primarySid = wi.FindFirst(ClaimTypes.PrimarySid)!.Value;
                            string samName    = winAccountName.Contains('\\') ? winAccountName.Split('\\')[1] : winAccountName;

                            logger.LogDebug("Building claims for {User} (local: {IsLocal}, scopes: {Scopes})",
                                winAccountName, isLocal, string.Join(" ", context.Request.GetScopes()));

                            if (isLocal)
                            {
                                if (context.Request.HasScope(Scopes.OpenId))
                                {
                                    identity.AddClaim(Claims.Subject, primarySid);
                                    identity.AddClaim(ClaimTypes.Name, samName);
                                }

                                if (context.Request.HasScope(Scopes.Profile))
                                {
                                    identity.AddClaim(ClaimTypes.WindowsAccountName, winAccountName);
                                }

                                if (context.Request.HasScope(Scopes.Email))
                                {
                                    identity.AddClaim(ClaimTypes.Email, samName + "@localhost");
                                }
                            }
                            else
                            {
                                // Fetch user attributes from Active Directory
                                using ADUser user = new ADUser(winAccountName);

                                if (context.Request.HasScope(Scopes.OpenId))
                                {
                                    identity.AddClaim(Claims.Subject, primarySid);
                                    identity.AddClaim(ClaimTypes.Name, user.DisplayName);
                                }

                                if (context.Request.HasScope(Scopes.Email))
                                {
                                    identity.AddClaim(ClaimTypes.Email, !string.IsNullOrEmpty(user.Email) ? user.Email : user.Username + "@localhost");
                                }

                                if (context.Request.HasScope(Scopes.Profile))
                                {
                                    identity.AddClaim(ClaimTypes.WindowsAccountName, winAccountName);
                                    if (!string.IsNullOrEmpty(user.GivenName)) { identity.AddClaim(ClaimTypes.GivenName, user.GivenName); }
                                    if (!string.IsNullOrEmpty(user.Surname)) { identity.AddClaim(ClaimTypes.Surname, user.Surname); }
                                    if (!string.IsNullOrEmpty(user.TelephoneNumber)) { identity.AddClaim(ClaimTypes.HomePhone, user.TelephoneNumber); }
                                }

                                if (context.Request.HasScope(Scopes.Roles))
                                {
                                    // Filter AD groups against the regex patterns in IdentityServer:Groups
                                    var groups = user.GroupsCommonName;
                                    foreach (string group in groups)
                                    {
                                        if (ValidGroupPatterns.Any(rx => rx.IsMatch(group)))
                                        {
                                            identity.AddClaim(ClaimTypes.Role, group);
                                        }
                                    }
                                    logger.LogDebug("User {User} has {Total} AD groups; {Matched} matched configured patterns",
                                        winAccountName, groups.Count, identity.FindAll(ClaimTypes.Role).Count());
                                }
                            }

                            // Include all claims in both the access token and the identity token
                            identity.SetDestinations(claim => new[]
                            {
                                Destinations.AccessToken,
                                Destinations.IdentityToken
                            });

                            context.Principal = new ClaimsPrincipal(identity);
                            logger.LogInformation("Authorization granted for {User}", winAccountName);
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Error building authorization claims for user '{User}'", winAccountName ?? "(unknown)");
                            context.Reject(error: Errors.ServerError, description: "An internal error occurred while processing the authorization request.");
                        }
                    }));
            })
            .AddValidation(options =>
            {
                options.UseLocalServer();
                options.UseAspNetCore();
            });
        }
    }
}
