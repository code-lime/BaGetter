namespace BaGetter.Git;

public sealed record GitRepositoryProgress(
    GitRepositoryProgressPhase Phase,
    int? Percent = null,
    long Completed = 0,
    long Total = 0,
    long ReceivedBytes = 0);
