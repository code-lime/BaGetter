using System.Collections.Generic;
using BaGetter.Git;
using Xunit;

namespace BaGetter.Tests;

public class GitRepositoryStatusTests
{
    [Fact]
    public void PublishesEachPhaseAndPercentOnce()
    {
        var target = new GitRepositoryStatus();
        var changes = new List<GitRepositoryProgress>();
        target.Changed += (_, progress) => changes.Add(progress);

        target.Report(new GitRepositoryProgress(
            GitRepositoryProgressPhase.Cloning,
            Percent: 25,
            Completed: 1,
            Total: 4,
            ReceivedBytes: 100));
        target.Report(new GitRepositoryProgress(
            GitRepositoryProgressPhase.Cloning,
            Percent: 25,
            Completed: 2,
            Total: 8,
            ReceivedBytes: 200));
        target.Report(new GitRepositoryProgress(
            GitRepositoryProgressPhase.Cloning,
            Percent: 26,
            Completed: 3,
            Total: 8,
            ReceivedBytes: 300));
        target.Report(new GitRepositoryProgress(
            GitRepositoryProgressPhase.Indexing,
            Percent: 26,
            Completed: 3,
            Total: 8));

        Assert.Collection(
            changes,
            progress =>
            {
                Assert.Equal(GitRepositoryProgressPhase.Cloning, progress.Phase);
                Assert.Equal(25, progress.Percent);
                Assert.Equal(100, progress.ReceivedBytes);
            },
            progress =>
            {
                Assert.Equal(GitRepositoryProgressPhase.Cloning, progress.Phase);
                Assert.Equal(26, progress.Percent);
            },
            progress =>
            {
                Assert.Equal(GitRepositoryProgressPhase.Indexing, progress.Phase);
                Assert.Equal(26, progress.Percent);
            });
        Assert.Same(changes[^1], target.Current);
    }
}
