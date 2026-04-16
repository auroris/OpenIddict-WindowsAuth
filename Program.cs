using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
			try
			{
				CreateHostBuilder(args).Build().Run();
			}
			catch (Exception ex)
			{
				// Log to stderr in case the logging providers are not yet configured.
				Console.Error.WriteLine($"[FATAL] Host terminated unexpectedly: {ex}");
				throw;
			}
		}

		public static IHostBuilder CreateHostBuilder(string[] args) =>
			Host.CreateDefaultBuilder(args)
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
			.ConfigureLogging(logging =>
			{
				logging.ClearProviders();
				logging.AddConsole();
				logging.SetMinimumLevel(LogLevel.Debug);
			})
			.ConfigureWebHostDefaults(webBuilder =>
			{
				webBuilder.UseStartup<Startup>();
			});
	}
}
