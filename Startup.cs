using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.IO;

namespace IdentityServer
{
	/// <summary>
	/// Configures services and the HTTP request pipeline.
	/// Sets up Windows (Negotiate/Kerberos) authentication, CORS, OpenIddict, and routing.
	/// </summary>
	public class Startup
	{
		/// <summary>
		/// Stores the <see cref="IConfiguration"/> in <see cref="Program.Configuration"/>.
		/// </summary>
		public Startup(IConfiguration configuration)
		{
			Program.Configuration = configuration;
		}

		/// <summary>
		/// Registers services: Negotiate authentication, authorization (with a fallback
		/// policy requiring authenticated users), CORS, OpenIddict via
		/// <see cref="IdentityServer.Add"/>, the <see cref="Authentication"/> middleware, and MVC controllers.
		/// </summary>
		public void ConfigureServices(IServiceCollection services)
		{
            services.AddAuthentication(NegotiateDefaults.AuthenticationScheme).AddNegotiate().AddCookie();
            services.AddAuthorization(options =>
            {
                options.FallbackPolicy = options.DefaultPolicy;
            });
            services.AddCors();
            IdentityServer.Add(services);
            services.AddSingleton<Authentication>();
            services.AddHttpContextAccessor();
            services.AddControllers();
        }

		/// <summary>
		/// Configures the middleware pipeline: CORS (origins from <c>IdentityServer:Hosts</c>),
		/// routing, the custom <see cref="Authentication"/> challenge middleware,
		/// ASP.NET Core authentication/authorization, and controller endpoints.
		/// </summary>
		public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILoggerFactory loggerFactory)
		{
			Program.LoggerFactory = loggerFactory;

			if (env.IsDevelopment())
			{
				app.UseDeveloperExceptionPage();
			}

			if (Program.Configuration.GetValue<bool>("IdentityServer:UseForwardedHeaders"))
			{
				app.UseForwardedHeaders();
			}

			// Configure CORS
			app.UseCors(builder =>
			{
				builder.WithOrigins(Program.Configuration.GetSection("IdentityServer:Hosts").Get<string[]>()!);
				builder.AllowAnyMethod();
				builder.AllowAnyHeader();
			});

            app.UseRouting();
            app.UseMiddleware<Authentication>();
            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
			{
				endpoints.MapControllers();
			});
		}
	}
}
