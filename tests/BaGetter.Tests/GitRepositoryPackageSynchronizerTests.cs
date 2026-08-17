using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BaGetter.Core;
using BaGetter.Git;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Versioning;
using Xunit;

namespace BaGetter.Tests;

public class GitRepositoryPackageSynchronizerTests
{
    [Fact]
    public async Task InitializeWaitsForFullPackageIndexing()
    {
        var indexingStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var continueIndexing = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var status = new GitRepositoryStatus();
        var progress = new List<GitRepositoryProgress>();
        status.Changed += (_, value) => progress.Add(value);
        var client = new Mock<IGitRepositoryClient>();
        client.Setup(repository => repository.UpdateAsync(It.IsAny<CancellationToken>())).ReturnsAsync("commit");
        client.Setup(repository => repository.GetRepositoryFilesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(["packages/testdata/1.2.3/testdata.1.2.3.nupkg"]);
        var storage = new Mock<IStorageService>();
        storage.Setup(service => service.GetAsync(
                "packages/testdata/1.2.3/testdata.1.2.3.nupkg",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => TestResources.GetResourceStream(TestResources.Package));
        var database = new Mock<IPackageDatabase>();
        database.Setup(db => db.ExistsAsync(
                It.IsAny<string>(),
                It.IsAny<NuGetVersion>(),
                It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                indexingStarted.TrySetResult();
                return await continueIndexing.Task;
            });
        database.Setup(db => db.AddAsync(It.IsAny<Package>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PackageAddResult.Success);
        var search = new Mock<ISearchIndexer>();
        search.Setup(index => index.IndexAsync(It.IsAny<Package>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var target = CreateTarget(
            "GitHub",
            client.Object,
            storage.Object,
            database.Object,
            search.Object,
            status);

        var initialize = target.InitializeAsync(CancellationToken.None);
        await indexingStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(initialize.IsCompleted);
        continueIndexing.SetResult(false);
        await initialize;

        database.Verify(db => db.AddAsync(It.IsAny<Package>(), It.IsAny<CancellationToken>()), Times.Once);
        search.Verify(index => index.IndexAsync(It.IsAny<Package>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(GitRepositoryProgressPhase.Checking, progress[0].Phase);
        Assert.Contains(progress, value => value.Phase == GitRepositoryProgressPhase.Indexing && value.Percent == 100);
        Assert.Equal(GitRepositoryProgressPhase.Synchronized, progress[^1].Phase);
    }

    [Fact]
    public async Task InitializeDoesNothingForOtherStorage()
    {
        var client = new Mock<IGitRepositoryClient>();
        var status = new GitRepositoryStatus();
        var target = CreateTarget(
            "FileSystem",
            client.Object,
            Mock.Of<IStorageService>(),
            Mock.Of<IPackageDatabase>(),
            Mock.Of<ISearchIndexer>(),
            status);

        await target.InitializeAsync(CancellationToken.None);

        client.Verify(repository => repository.UpdateAsync(It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal(GitRepositoryProgressPhase.Idle, status.Current.Phase);
    }

    [Fact]
    public async Task InitializePropagatesFailureAndMarksStatusFailed()
    {
        var client = new Mock<IGitRepositoryClient>();
        client.Setup(repository => repository.UpdateAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Git unavailable"));
        var status = new GitRepositoryStatus();
        var target = CreateTarget(
            "GitHub",
            client.Object,
            Mock.Of<IStorageService>(),
            Mock.Of<IPackageDatabase>(),
            Mock.Of<ISearchIndexer>(),
            status);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            target.InitializeAsync(CancellationToken.None));

        Assert.Equal("Git unavailable", exception.Message);
        Assert.Equal(GitRepositoryProgressPhase.Failed, status.Current.Phase);
    }

    private static GitRepositoryPackageSynchronizer CreateTarget(
        string storageType,
        IGitRepositoryClient client,
        IStorageService storage,
        IPackageDatabase database,
        ISearchIndexer search,
        GitRepositoryStatus status)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["Storage:Type"] = storageType,
            })
            .Build();
        var options = new Mock<IOptionsSnapshot<GitRepositoryOptions>>();
        options.Setup(value => value.Value).Returns(new GitRepositoryOptions
        {
            Owner = Guid.NewGuid().ToString("N"),
            Repository = "packages",
            Branch = "main",
        });

        return new GitRepositoryPackageSynchronizer(
            client,
            storage,
            database,
            search,
            new SystemTime(),
            configuration,
            options.Object,
            NullLogger<GitRepositoryPackageSynchronizer>.Instance,
            status);
    }
}
