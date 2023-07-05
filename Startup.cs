using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace IdentityServer
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Program.Configuration = configuration;
        }

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

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            // Configure CORS
            app.UseCors(builder =>
            {
                builder.WithOrigins(Program.Configuration.GetSection("IdentityServer:Hosts").Get<string[]>());
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
