using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using System;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using static OpenIddict.Abstractions.OpenIddictConstants;
using static OpenIddict.Server.OpenIddictServerEvents;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using User = IdentityServer.ActiveDirectory.User;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Server.IISIntegration;

namespace IdentityServer
{
    public class IdentityServer
    {
        public static void Add(IServiceCollection services)
        {
            // Attach OpenIddict with a ton of options
            services.AddOpenIddict().AddServer(options =>
            {
                // This OpenIddict server is stateless; however, make sure IIS doesn't dispose of the application too often (ie, via app pool recycles or shut downs due to inactivity)
                options.AddEphemeralEncryptionKey().AddEphemeralSigningKey();
                options.AllowAuthorizationCodeFlow();
                options.AllowImplicitFlow();
                options.SetIssuer(new Uri(Program.Configuration.GetSection("IdentityServer:ServerUri").Get<string>()))
                       .SetAuthorizationEndpointUris("connect/authorize")
                       .SetTokenEndpointUris("connect/token");
                options.EnableDegradedMode(); // We'll handle authentication and claims ourselves; don't want user stores or such
                options.UseAspNetCore()
                    .DisableTransportSecurityRequirement(); // Disable the need for HTTPS in dev
                options.RegisterScopes(Scopes.OpenId, Scopes.Email, Scopes.Profile, Scopes.Roles); // Tell OpenIddict that we support these scopes

                // Event handler for validating authorization requests
                options.AddEventHandler<ValidateAuthorizationRequestContext>(builder =>
                    builder.UseInlineHandler(context =>
                    {
                        // Verification: I accept all context.ClientId's, but do check to see if the context.RedirectUri is proper
                        // Partial matches, case insensitive
                        if (Program.Configuration.GetSection("IdentityServer:Hosts").Get<string[]>().Any(s => context.RedirectUri.Contains(s, StringComparison.InvariantCultureIgnoreCase)))
                        {
                            return default;
                        }

                        // Fall-through: URL was not proper.
                        context.Reject(
                            error: Errors.InvalidClient,
                            description: "The specified redirect_uri " + context.RedirectUri + " is not valid. Check the IdentityServer:Hosts key in appsettings.json for valid values.");
                        return default;
                    }));

                // Event handler for validating token requests
                options.AddEventHandler<ValidateTokenRequestContext>(builder =>
                    builder.UseInlineHandler(context =>
                    {
                        // I accept all context.ClientId's, so just carry on.
                        return default;
                    }));

                // Event handler for authorization requests
                options.AddEventHandler<HandleAuthorizationRequestContext>(builder =>
                    builder.UseInlineHandler(async context =>
                    {
                        HttpRequest request = context.Transaction.GetHttpRequest() ?? throw new InvalidOperationException("The ASP.NET Core request cannot be retrieved.");
                        AuthenticateResult result = await request.HttpContext.AuthenticateAsync(IISDefaults.AuthenticationScheme);
                        if (!result.Succeeded) { throw new Exception("Could not authenticate."); }

                        ClaimsIdentity identity = new ClaimsIdentity(TokenValidationParameters.DefaultAuthenticationType);
                        WindowsIdentity wi = (WindowsIdentity)request.HttpContext.User.Identity;

                        // If the user is a member of the local login users group, then this is a locally logged on user and likely Active Directory is not available
                        bool isLocal = wi.FindAll(ClaimTypes.GroupSid).Where(g => g.Value == "S-1-2-0").Count() > 0;

                        if (isLocal)
                        {
                            if (context.Request.HasScope(Scopes.OpenId))
                            {
                                // Add the name identifier claim; this is the user's unique identifier
                                identity.AddClaim(Claims.Subject, wi.FindFirst(ClaimTypes.PrimarySid).Value);

                                // Add the account's friendly name
                                identity.AddClaim(ClaimTypes.Name, wi.FindFirst(ClaimTypes.Name).Value.Split("\\")[1]);
                            }

                            if (context.Request.HasScope(Scopes.Profile))
                            {
                                // Add the user's windows username
                                identity.AddClaim(ClaimTypes.WindowsAccountName, wi.FindFirst(ClaimTypes.Name).Value);
                            }

                            if (context.Request.HasScope(Scopes.Email))
                            {
                                // Add the email address
                                identity.AddClaim(ClaimTypes.Email, wi.FindFirst(ClaimTypes.Name).Value.Split("\\")[1] + "@localhost");
                            }
                        }
                        else
                        {
                            // Get information about the user
                            User user = new User(wi.FindFirst(ClaimTypes.Name).Value);

                            // Attach basic id if requested
                            if (context.Request.HasScope(Scopes.OpenId))
                            {
                                // Add the name identifier claim; this is the user's unique identifier
                                identity.AddClaim(Claims.Subject, wi.FindFirst(ClaimTypes.PrimarySid).Value);

                                // Add the account's friendly name
                                identity.AddClaim(ClaimTypes.Name, user.DisplayName);
                            }

                            // Attach email address if requested
                            if (context.Request.HasScope(Scopes.Email))
                            {
                                // Add the user's email address
                                if (user.Email != null)
                                {
                                    identity.AddClaim(ClaimTypes.Email, user.Email);
                                }
                                else
                                {
                                    identity.AddClaim(ClaimTypes.Email, user.Username + "@localhost");
                                }
                            }

                            // Attach profile stuff if requested
                            if (context.Request.HasScope(Scopes.Profile))
                            {
                                // Add the user's windows username
                                identity.AddClaim(ClaimTypes.WindowsAccountName, wi.FindFirst(ClaimTypes.Name).Value);

                                // Add the user's name
                                if (user.FirstName != null) { identity.AddClaim(ClaimTypes.GivenName, user.FirstName); }
                                if (user.LastName != null) { identity.AddClaim(ClaimTypes.Surname, user.LastName); }

                                // Telephone #
                                if (user.TelephoneNumber != null) { identity.AddClaim(ClaimTypes.HomePhone, user.TelephoneNumber); }
                            }

                            // Attach roles if requested
                            if (context.Request.HasScope(Scopes.Roles))
                            {
                                Regex[] validGroups = Program.Configuration.GetSection("IdentityServer:Groups").Get<string[]>().Select(group => new Regex(group, RegexOptions.IgnoreCase)).ToArray();
                                HashSet<string> identityGroups = new HashSet<string>();

                                // Get group claims, filter duplicates
                                foreach (String group in user.GroupsCommonName)
                                {
                                    foreach (Regex rx in validGroups)
                                    {
                                        if (rx.Matches(group).Count > 0)
                                        {
                                            identityGroups.Add(group);
                                        }
                                    }
                                }

                                // Add the groups to the claims
                                foreach (string group in identityGroups)
                                {
                                    identity.AddClaim(ClaimTypes.Role, group);
                                }
                            }
                        }

                        // Allow all claims to be expressed in both the access token and identity token; see https://documentation.openiddict.com/configuration/claim-destinations.html
                        identity.SetDestinations(claim => new[]
                        {
                            Destinations.AccessToken,
                            Destinations.IdentityToken
                        });

                        // Attach the claims principal to the authorization context so OpenIddict can send a response
                        context.Principal = new ClaimsPrincipal(identity);
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
