using System;
using System.Collections.Generic;
using BaGetter.Core;
using BaGetter.Database.Sqlite;
using BaGetter.Git;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace BaGetter.Tests;

public class HostIntegrationTests
{
    private readonly string DatabaseTypeKey = "Database:Type";
    private readonly string ConnectionStringKey = "Database:ConnectionString";
    private readonly string StorageTypeKey = "Storage:Type";

    [Fact]
    public void ThrowsIfDatabaseTypeInvalid()
    {
        var provider = BuildServiceProvider(new Dictionary<string, string>
        {
            { DatabaseTypeKey, "InvalidType" }
        });

        Assert.Throws<InvalidOperationException>(
            () => provider.GetRequiredService<IContext>());
    }

    [Fact]
    public void ReturnsDatabaseContext()
    {
        var provider = BuildServiceProvider(new Dictionary<string, string>
        {
            { DatabaseTypeKey, "Sqlite" },
            { ConnectionStringKey, "..." }
        });

        Assert.NotNull(provider.GetRequiredService<IContext>());
    }

    [Fact]
    public void ReturnsSqliteContext()
    {
        var provider = BuildServiceProvider(new Dictionary<string, string>
        {
            { DatabaseTypeKey, "Sqlite" },
            { ConnectionStringKey, "..." }
        });

        Assert.NotNull(provider.GetRequiredService<SqliteContext>());
    }

    [Fact]
    public void DefaultsToSqlite()
    {
        var provider = BuildServiceProvider();

        var context = provider.GetRequiredService<IContext>();

        Assert.IsType<SqliteContext>(context);
    }

    [Fact]
    public void UsesProductionLogLevels()
    {
        using var host = Program.CreateHostBuilder(Array.Empty<string>()).Build();
        var loggerFactory = host.Services.GetRequiredService<ILoggerFactory>();
        var framework = loggerFactory.CreateLogger("Microsoft.EntityFrameworkCore.Database.Command");
        var lifetime = loggerFactory.CreateLogger("Microsoft.Hosting.Lifetime");
        var git = loggerFactory.CreateLogger("BaGetter.Git.LibGit2SharpRepositoryClient");

        Assert.False(framework.IsEnabled(LogLevel.Information));
        Assert.True(framework.IsEnabled(LogLevel.Warning));
        Assert.True(lifetime.IsEnabled(LogLevel.Information));
        Assert.False(git.IsEnabled(LogLevel.Debug));
        Assert.True(git.IsEnabled(LogLevel.Information));
    }

    [Fact]
    public void ReturnsGitRepositoryStorage()
    {
        var provider = BuildServiceProvider(new Dictionary<string, string>
        {
            { StorageTypeKey, "GitHub" },
            { "Storage:Owner", "owner" },
            { "Storage:Repository", "repository" },
        });

        var storage = provider.GetRequiredService<IStorageService>();

        Assert.IsType<GitRepositoryService>(storage);
    }

    [Fact]
    public void ReturnsGitRepositoryPackageSynchronizer()
    {
        using var host = Program
            .CreateHostBuilder(Array.Empty<string>())
            .ConfigureAppConfiguration((ctx, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string>
                {
                    { StorageTypeKey, "GitHub" },
                    { "Storage:Owner", "owner" },
                    { "Storage:Repository", "repository" },
                });
            })
            .Build();
        using var scope = host.Services.CreateScope();

        var synchronizer = scope.ServiceProvider.GetRequiredService<GitRepositoryPackageSynchronizer>();

        Assert.NotNull(synchronizer);
    }

    private IServiceProvider BuildServiceProvider(Dictionary<string, string> configs = null)
    {
        var host = Program
            .CreateHostBuilder(Array.Empty<string>())
            .ConfigureAppConfiguration((ctx, config) =>
            {
                config.AddInMemoryCollection(configs ?? new Dictionary<string, string>());
            })
            .Build();

        return host.Services;
    }
}
