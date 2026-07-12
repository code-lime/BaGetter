using System;
using BaGetter.Core;
using BaGetter.Git;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BaGetter;

public static class GitRepositoryApplicationExtensions
{
    public static BaGetterApplication AddGitRepository(this BaGetterApplication app)
    {
        app.Services.AddBaGetterOptions<GitRepositoryOptions>(nameof(BaGetterOptions.Storage));
        app.Services.AddSingleton<IGitRepositoryClient, LibGit2SharpRepositoryClient>();
        app.Services.AddHealthChecks()
            .AddCheck<GitRepositoryHealthCheck>("GitHub", tags: ["GitHub"]);
        app.Services.AddTransient<IPackageStorageSynchronizer, GitRepositoryPackageSynchronizer>();
        app.Services.AddTransient<GitRepositoryService>();

        app.Services.TryAddTransient<IStorageService>(provider => provider.GetRequiredService<GitRepositoryService>());

        app.Services.AddProvider<IStorageService>((provider, config) =>
        {
            if (!config.HasStorageType("GitHub"))
                return null;
            return provider.GetRequiredService<GitRepositoryService>();
        });

        return app;
    }

    public static BaGetterApplication AddGitRepository(
        this BaGetterApplication app,
        Action<GitRepositoryOptions> configure)
    {
        app.AddGitRepository();
        app.Services.Configure(configure);
        return app;
    }
}
