using Microsoft.Extensions.DependencyInjection;
using System;
using FluentMigrator.Runner;
using Microsoft.Extensions.Configuration;
using DbMigrations.Extensions;
using Serilog;
using System.IO;
using System.Diagnostics;
using Bookit.Core;
using Microsoft.Extensions.Logging;

namespace DbMigrations
{
    class Program
    {
        public static IConfigurationRoot Configuration { get; private set; }
        public static string LogWriteFilePath = string.Empty;
        public static readonly bool IsDevelopment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";

        private static readonly ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddSerilog();
        });
        private static readonly ILogger<Program> logger = loggerFactory.CreateLogger<Program>();

        static int Main(string[] args)
        {
            ConfigureLoggingBootstrap();
            try
            {
                ConfigureAppConfiguration(args);
                ConfigureLogging();
                var serviceProvider = CreateServices();

                // Put the database update into a scope to ensure
                // that all resources will be disposed.
                using var scope = serviceProvider.CreateScope();
                UpdateDatabase(scope.ServiceProvider);
                return 0;
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "DbMigration terminated unexpectedly");
                return 1;
            }
            finally
            {
                Log.CloseAndFlush();
                bool needOpenNotePad = !string.IsNullOrEmpty(LogWriteFilePath) && args.Length == 0;
                if (needOpenNotePad)
                {
                    // Opens notepad only if no args specified (when ran on build server - should not open notepad)
                    Process.Start(@"notepad.exe", LogWriteFilePath);
                }
            }

        }

        static void ConfigureAppConfiguration(string[] args)
        {
            var builder = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", true, true)
                .AddJsonFile($"secrets/appsettings.{CommonConstants.EnvironmentName}.secret.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .AddCommandLine(args)
                .AddAWSParamStore(logger);
            if (IsDevelopment)
            {
                builder.AddUserSecrets<Program>();
            }
            Configuration = builder.Build();
            if (!string.IsNullOrEmpty(LogWriteFilePath))
            {
                Log.Logger.Information($"LogFilePath: {LogWriteFilePath}");
            }
        }

        /// <summary>
        /// Configure the dependency injection services
        /// </summary>
        private static IServiceProvider CreateServices()
        {
            var sqlCommandTimeout = TimeSpan.FromSeconds(Configuration.GetInt("DbMigration:SqlCommandTimeoutSec", 1800));
            return new ServiceCollection()
                // Add common FluentMigrator services
                .AddFluentMigratorCore()
                // Enable logging to console in the FluentMigrator way
                .AddLogging(lb => lb.AddFluentMigratorConsole().AddSerilog(Log.Logger))
                .ConfigureRunner(rb => rb
                    // Add SqlServer support to FluentMigrator
                    .AddPostgres()
                    .WithGlobalConnectionString(Configuration.GetConnectionString("DefaultConnection"))
                    .WithGlobalCommandTimeout(sqlCommandTimeout)
                    .ScanIn(typeof(Program).Assembly).For.Migrations().For.EmbeddedResources())
                // Build the service provider
                .BuildServiceProvider(false);
        }

        /// <summary>
        /// Update the database
        /// </summary>
        private static void UpdateDatabase(IServiceProvider serviceProvider)
        {
            if (Configuration.GetConnectionString("DefaultConnection") == null)
            {
                throw new ArgumentNullException("appsettings.DefaultConnection");
            }
            // Instantiate the runner
            var runner = serviceProvider.GetRequiredService<IMigrationRunner>();
            Log.Information("--------- Migration started ---------");

            // Execute the migrations
            runner.MigrateUp();
            Log.Information("--------- Migration finished ---------");
        }

        static void ConfigureLoggingBootstrap()
        {
            Serilog.Log.Logger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .WriteTo.Console(outputTemplate: CommonConstants.LOG_OUTPUT_TEMPLATE)
                .CreateLogger();
        }

        static void ConfigureLogging()
        {
            var logFilePath = Configuration["DbMigration:LogFilePath"];
            var loggerConfig = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .WriteTo.Console(outputTemplate: CommonConstants.LOG_OUTPUT_TEMPLATE);
            if (!string.IsNullOrEmpty(logFilePath))
            {
                var ext = Path.GetExtension(logFilePath);
                LogWriteFilePath = Path.ChangeExtension(logFilePath, $"[{DateTime.Now:dd-MM-yy HH-mm-ss}]{ext}");
                loggerConfig.WriteTo.File(LogWriteFilePath, outputTemplate: CommonConstants.LOG_OUTPUT_TEMPLATE, rollingInterval: RollingInterval.Infinite);
            }
            Serilog.Log.Logger = loggerConfig.CreateLogger();
        }
    }
}
