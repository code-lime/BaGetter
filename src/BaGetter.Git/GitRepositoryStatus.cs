using System;

namespace BaGetter.Git;

public sealed class GitRepositoryStatus : IProgress<GitRepositoryProgress>
{
    private readonly object _lock = new();
    private GitRepositoryProgress _current = new(GitRepositoryProgressPhase.Idle);

    public event EventHandler<GitRepositoryProgress> Changed;

    public GitRepositoryProgress Current
    {
        get
        {
            lock (_lock)
            {
                return _current;
            }
        }
    }

    public void Report(GitRepositoryProgress value)
    {
        ArgumentNullException.ThrowIfNull(value);

        EventHandler<GitRepositoryProgress> changed;
        lock (_lock)
        {
            if (_current.Phase == value.Phase && _current.Percent == value.Percent)
            {
                return;
            }

            _current = value;
            changed = Changed;
        }

        changed?.Invoke(this, value);
    }
}
