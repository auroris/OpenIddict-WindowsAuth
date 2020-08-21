using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using System;
using System.DirectoryServices.AccountManagement;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using static OpenIddict.Abstractions.OpenIddictConstants;
using static OpenIddict.Server.OpenIddictServerEvents;

namespace IdentityServer
{
	public class Startup
	{
		/// <summary>
		/// Configuration comes from appsettings.json, environment variables, or the command line.
		/// </summary>
		private readonly IConfiguration Configuration;

		public Startup(IConfiguration configuration)
		{
			Configuration = configuration;
		}

		public void ConfigureServices(IServiceCollection services)
		{
			services.AddControllers();
			services.AddAuthentication().AddCookie();

			// Attach OpenIddict with a ton of options
			services.AddOpenIddict().AddServer(options =>
			{
				// This OpenIddict server is stateless; however, make sure IIS doesn't dispose of the application too often (ie, via app pool recycles or shut downs due to inactivity)
				options.AddEphemeralEncryptionKey().AddEphemeralSigningKey();
				options.AllowAuthorizationCodeFlow();
				options.AllowImplicitFlow();
				options.SetAuthorizationEndpointUris("/connect/authorize")
					   .SetTokenEndpointUris("/connect/token");
				options.EnableDegradedMode(); // We'll handle protocol stuff ourselves; don't want user stores or such
				options.UseAspNetCore()
					.DisableTransportSecurityRequirement(); // Disable the need for HTTPS in dev
				options.RegisterScopes(Scopes.OpenId, Scopes.Email, Scopes.Profile, Scopes.Roles); // Tell OpenIddict that we support these scopes

				// Event handler for validating authorization requests
				options.AddEventHandler<ValidateAuthorizationRequestContext>(builder =>
					builder.UseInlineHandler(context =>
					{
						// Verification: I accept all context.ClientId's, but do check to see if the context.RedirectUri is proper
						// Partial matches, case insensitive
						if (Configuration.GetSection("IdentityServer:Hosts").Get<string[]>().Any(s => context.RedirectUri.Contains(s, StringComparison.OrdinalIgnoreCase)))
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
						// Get the HTTP request
						HttpRequest request = context.Transaction.GetHttpRequest() ?? throw new InvalidOperationException("The ASP.NET Core request cannot be retrieved.");

						// Try to get the authentication of the current session via Windows Authentication
						AuthenticateResult result = await request.HttpContext.AuthenticateAsync("Windows");

						if (result?.Principal is WindowsPrincipal wp) // If we're authenticated using Windows authentication, build an Identity with Claims;
						{
							PrincipalContext directoryService;
							ClaimsIdentity identity = new ClaimsIdentity(TokenValidationParameters.DefaultAuthenticationType);

							// Set the directory service to the active directory domain preferrably; but the machine is okay too for dev if we're not connected to AD
							try
							{
								directoryService = new PrincipalContext(ContextType.Domain);
							}
							catch (Exception)
							{
								directoryService = new PrincipalContext(ContextType.Machine);
							}

							// Get information about the user
							UserPrincipal user = UserPrincipal.FindByIdentity(directoryService, result.Principal.FindFirstValue(ClaimTypes.Name));

							// Attach basic id if requested
							if (context.Request.HasScope(Scopes.OpenId))
							{
								// Add the name identifier claim; this is the user's unique identifier
								identity.AddClaim(Claims.Subject, result.Principal.FindFirstValue(ClaimTypes.PrimarySid), Destinations.AccessToken);

								// Add the account's friendly name
								identity.AddClaim(ClaimTypes.Name, user.DisplayName, Destinations.IdentityToken);
							}

							// Attach email address if requested
							if (context.Request.HasScope(Scopes.Email))
							{
								// Add the user's email address
								if (user.EmailAddress != null)
								{
									identity.AddClaim(ClaimTypes.Email, user.EmailAddress, Destinations.IdentityToken);
								}
								else
								{
									// A common requirement for many apps using OpenID Connect is the user's email address. If the account is a machine account,
									// like in dev, it won't be available, so just make something up
									identity.AddClaim(ClaimTypes.Email, CreateMD5(user.DisplayName) + "@dev.localhost", Destinations.IdentityToken);
								}
							}

							// Attach profile stuff if requested
							if (context.Request.HasScope(Scopes.Profile))
							{
								// Add the user's windows username
								identity.AddClaim(ClaimTypes.WindowsAccountName, result.Principal.FindFirstValue(ClaimTypes.Name), Destinations.IdentityToken);

								// Add the user's name
								if (user.GivenName != null) { identity.AddClaim(ClaimTypes.GivenName, user.GivenName, Destinations.IdentityToken); }
								if (user.Surname != null) { identity.AddClaim(ClaimTypes.Surname, user.Surname, Destinations.IdentityToken); }

								// Telephone #
								if (user.VoiceTelephoneNumber != null) { identity.AddClaim(ClaimTypes.HomePhone, user.VoiceTelephoneNumber, Destinations.IdentityToken); }
							}

							// Attach roles if requested
							if (context.Request.HasScope(Scopes.Roles))
							{
								// Get and assign the group claims
								foreach (Principal group in user.GetGroups())
								{
									// Limit the groups returned to the smallest set possible. Specify by name, exact match and case insensitive.
									if (group.Name != null && Configuration.GetSection("IdentityServer:Groups").Get<string[]>().Contains(group.Name, StringComparer.OrdinalIgnoreCase))
									{
										identity.AddClaim(ClaimTypes.Role, group.Name, Destinations.IdentityToken);
									}
								}
							}

							// Attach the principal to the authorization context, so that an OpenID Connect response
							// with an authorization code can be generated by the OpenIddict server services.
							context.Principal = new ClaimsPrincipal(identity);
						}
						else
						{
							// Run Windows authentication
							await request.HttpContext.ChallengeAsync("Windows");
							context.HandleRequest();
						}
					}));
			})
			.AddValidation(options =>
			{
				options.UseLocalServer();
				options.UseAspNetCore();
			});

			// Add cross-origin resource sharing for Javascript clients
			services.AddCors();
		}

		public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
		{
			if (env.IsDevelopment())
			{
				app.UseDeveloperExceptionPage();
			}

			// Configure CORS
			app.UseCors(builder =>
			{
				builder.WithOrigins(Configuration.GetSection("IdentityServer:Hosts").Get<string[]>());
				builder.AllowAnyMethod();
				builder.AllowAnyHeader();
			});

			app.UseRouting();
			app.UseAuthentication();
			app.UseAuthorization();

			// If you add any additional API's
			app.UseEndpoints(endpoints =>
			{
				endpoints.MapControllers();
			});
		}

		/// <summary>
		/// Helper method to create an md5 hash from a string
		/// </summary>
		/// <param name="input">a string</param>
		/// <returns>hex-encoded md5 of string</returns>
		public static string CreateMD5(string input)
		{
			// Use input string to calculate MD5 hash
			using (System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create())
			{
				byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(input);
				byte[] hashBytes = md5.ComputeHash(inputBytes);

				// Convert the byte array to hexadecimal string
				System.Text.StringBuilder sb = new System.Text.StringBuilder();
				for (int i = 0; i < hashBytes.Length; i++)
				{
					sb.Append(hashBytes[i].ToString("X2"));
				}
				return sb.ToString();
			}
		}
	}
}
