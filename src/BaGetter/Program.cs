using System;
using System.IO;
using System.Threading.Tasks;
using BaGetter.Core;
using BaGetter.Git;
using BaGetter.Web;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.EventLog;
using Serilog;
using Serilog.Events;

namespace BaGetter;

public class Program
{
    public static async Task Main(string[] args)
    {
        using var host = CreateHostBuilder(args).Build();
        await RunAsync(host, args);
    }

    public static async Task RunAsync(IHost host, string[] args)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(args);

        var logger = host.Services.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("BaGetter host initializing from {BaseDirectory}", AppContext.BaseDirectory);

        if (!host.ValidateStartupOptions())
        {
            return;
        }

        var app = new CommandLineApplication
        {
            Name = "baget",
            Description = "A light-weight NuGet service",
        };

        app.HelpOption(inherited: true);

        app.Command("import", import =>
        {
            import.Command("downloads", downloads =>
            {
                downloads.OnExecuteAsync(async cancellationToken =>
                {
                    using var scope = host.Services.CreateScope();
                    var importer = scope.ServiceProvider.GetRequiredService<DownloadsImporter>();

                    await importer.ImportAsync(cancellationToken);
                });
            });
        });

        app.Option("--urls", "The URLs that BaGetter should bind to.", CommandOptionType.SingleValue);

        app.OnExecuteAsync(async cancellationToken =>
        {
            await host.RunMigrationsAsync(cancellationToken);

            using (var scope = host.Services.CreateScope())
            {
                var synchronizer = scope.ServiceProvider.GetRequiredService<GitRepositoryPackageSynchronizer>();
                await synchronizer.InitializeAsync(cancellationToken);
            }

            await host.RunAsync(cancellationToken);
        });

        await app.ExecuteAsync(args);
    }

    public static IHostBuilder CreateHostBuilder(string[] args)
    {
        return Host
            .CreateDefaultBuilder(args)
            .UseContentRoot(AppContext.BaseDirectory)
            .ConfigureAppConfiguration((ctx, config) =>
            {
                var root = Environment.GetEnvironmentVariable("BAGET_CONFIG_ROOT");

                if (!string.IsNullOrEmpty(root))
                {
                    config.SetBasePath(root);
                }

                // Optionally load secrets from files in the conventional path
                config.AddKeyPerFile("/run/secrets", optional: true);
                config.AddEnvironmentVariablePlaceholders();
            })
            .ConfigureLogging((context, logging) =>
            {
                // A desktop process normally cannot create Event Log sources without elevation.
                if (OperatingSystem.IsWindows())
                {
                    logging.AddFilter<EventLogLoggerProvider>(_ => false);
                }

                var logPath = context.Configuration["Logging:File:Path"];
                if (string.IsNullOrWhiteSpace(logPath))
                {
                    logPath = "logs/bagetter.log";
                }

                if (!Path.IsPathFullyQualified(logPath))
                {
                    logPath = Path.GetFullPath(logPath, AppContext.BaseDirectory);
                }

                Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

                var fileLogger = new LoggerConfiguration()
                    .MinimumLevel.Information()
                    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                    .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
                    .Enrich.FromLogContext()
                    .WriteTo.File(
                        logPath,
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 3,
                        shared: true,
                        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
                    .CreateLogger();

                logging.AddSerilog(fileLogger, dispose: true);
            })
            .ConfigureWebHostDefaults(web =>
            {
                web.ConfigureKestrel(options =>
                {
                    // Remove the upload limit from Kestrel. If needed, an upload limit can
                    // be enforced by a reverse proxy server.
                    options.Limits.MaxRequestBodySize = null;
                });

                web.UseStartup<Startup>();
            });
    }
}
