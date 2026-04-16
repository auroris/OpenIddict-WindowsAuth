using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using System;

namespace IdentityServer
{
	/// <summary>
	/// Application entry point. Builds and runs the web host
	/// with configuration from <c>appsettings.json</c>, environment variables, and
	/// command-line arguments.
	/// </summary>
	public class Program
	{
		/// <summary>
		/// Global configuration instance, populated by <see cref="Startup"/> during host initialization.
		/// Used throughout the application to read <c>IdentityServer</c> and <c>OracleSSO</c> settings.
		/// </summary>
		public static IConfiguration Configuration = null!;

		/// <summary>
		/// Global logger factory, set by <see cref="Startup.Configure"/> once the host is built.
		/// Used by classes outside the DI container (e.g. Active Directory helpers) to obtain loggers.
		/// </summary>
		public static ILoggerFactory? LoggerFactory;

		public static void Main(string[] args)
		{
			// Bootstrap logger used only until the host reads appsettings.json.
			Log.Logger = new LoggerConfiguration()
				.MinimumLevel.Warning()
				.WriteTo.Console()
				.CreateLogger();

			try
			{
				CreateHostBuilder(args).Build().Run();
			}
			catch (Exception ex)
			{
				Log.Fatal(ex, "Host terminated unexpectedly");
				throw;
			}
			finally
			{
				Log.CloseAndFlush();
			}
		}

		public static IHostBuilder CreateHostBuilder(string[] args) =>
			Host.CreateDefaultBuilder(args)
			.UseSerilog((context, loggerConfig) => loggerConfig
				.ReadFrom.Configuration(context.Configuration)
				.Enrich.FromLogContext()
				.WriteTo.Console()
				.WriteTo.File("logs/identityserver-.log",
					rollingInterval: RollingInterval.Day,
					retainedFileCountLimit: 14,
					outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}"))
			.ConfigureAppConfiguration((hostingContext, config) =>
			{
				config.Sources.Clear();
				config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
				config.AddEnvironmentVariables();
				if (args != null)
				{
					config.AddCommandLine(args);
				}
			})
			.ConfigureWebHostDefaults(webBuilder =>
			{
				webBuilder.UseStartup<Startup>();
			});
	}
}
