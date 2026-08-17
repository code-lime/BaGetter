namespace BaGetter.Git;

public enum GitRepositoryProgressPhase
{
    Idle,
    Checking,
    Cloning,
    Fetching,
    Indexing,
    Synchronized,
    Failed,
}
