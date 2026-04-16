using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
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
    /// Supported flows: Authorization Code, Implicit, and Hybrid.
    /// Supported scopes: <c>openid</c>, <c>email</c>, <c>profile</c>, <c>roles</c>.
    /// </para>
    /// <para>
    /// When <c>IdentityServer:PersistKeys</c> is <see langword="true"/>, signing and encryption keys
    /// are persisted to disk (under <c>IdentityServer:DataPath</c>), protected at rest with Windows
    /// DPAPI, so the same key material survives app pool recycles and avoids JWKS cache mismatches
    /// in OIDC clients. When <see langword="false"/> (default), ephemeral keys are generated on
    /// every startup.
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
        /// Loads a persistent RSA key from <paramref name="filename"/> under <c>IdentityServer:DataPath</c>
        /// (defaulting to a <c>keys</c> subfolder of the app base directory), generating and saving a new
        /// 2048-bit key if the file does not yet exist.
        /// </summary>
        /// <remarks>
        /// Private key material is encrypted at rest with Windows DPAPI
        /// (<see cref="DataProtectionScope.LocalMachine"/>); the raw key bytes are never written to disk
        /// in plaintext. Only processes running on the same machine can decrypt the file.
        /// </remarks>
        private static RsaSecurityKey LoadOrCreateRsaKey(string filename)
        {
            var dataPath = Program.Configuration.GetValue<string>("IdentityServer:DataPath")
                           ?? Path.Combine(AppContext.BaseDirectory, "keys");
            Directory.CreateDirectory(dataPath);
            var keyPath = Path.Combine(dataPath, filename);

            var rsa = RSA.Create(2048);
            if (File.Exists(keyPath))
            {
                byte[] decrypted = ProtectedData.Unprotect(
                    File.ReadAllBytes(keyPath), null, DataProtectionScope.LocalMachine);
                rsa.ImportFromPem(Encoding.UTF8.GetString(decrypted));
            }
            else
            {
                byte[] pem = Encoding.UTF8.GetBytes(rsa.ExportPkcs8PrivateKeyPem());
                File.WriteAllBytes(keyPath, ProtectedData.Protect(pem, null, DataProtectionScope.LocalMachine));
            }

            return new RsaSecurityKey(rsa);
        }

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
                // When PersistKeys is true, keys survive app pool recycles (preventing JWKS cache
                // mismatches) and are protected at rest with Windows DPAPI. When false (default),
                // ephemeral keys are generated on every startup.
                if (Program.Configuration.GetValue<bool>("IdentityServer:PersistKeys"))
                    options.AddSigningKey(LoadOrCreateRsaKey("signing-key.bin"))
                           .AddEncryptionKey(LoadOrCreateRsaKey("encryption-key.bin"));
                else
                    options.AddSigningKey(new RsaSecurityKey(RSA.Create(2048)))
                           .AddEncryptionKey(new RsaSecurityKey(RSA.Create(2048)));
                options.AllowAuthorizationCodeFlow();
                options.AllowHybridFlow();
                options.AllowImplicitFlow();

                var accessTokenLifetime = Program.Configuration.GetValue<TimeSpan?>("IdentityServer:AccessTokenLifetime");
                if (accessTokenLifetime.HasValue) options.SetAccessTokenLifetime(accessTokenLifetime.Value);

                var identityTokenLifetime = Program.Configuration.GetValue<TimeSpan?>("IdentityServer:IdentityTokenLifetime");
                if (identityTokenLifetime.HasValue) options.SetIdentityTokenLifetime(identityTokenLifetime.Value);

                var authCodeLifetime = Program.Configuration.GetValue<TimeSpan?>("IdentityServer:AuthorizationCodeLifetime");
                if (authCodeLifetime.HasValue) options.SetAuthorizationCodeLifetime(authCodeLifetime.Value);
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
