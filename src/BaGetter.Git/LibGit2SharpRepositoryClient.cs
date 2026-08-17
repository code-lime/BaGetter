using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGetter.Core;
using LibGit2Sharp;
using LibGit2Sharp.Handlers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BaGetter.Git;

/// <summary>Maintains a local working copy and performs all Git operations through LibGit2Sharp.</summary>
public sealed class LibGit2SharpRepositoryClient : IGitRepositoryClient, IDisposable
{
    private readonly GitRepositoryOptions _options;
    private readonly ILogger<LibGit2SharpRepositoryClient> _logger;
    private readonly IProgress<GitRepositoryProgress> _progress;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly string _workPath;
    private readonly string _repositoryUrl;

    public LibGit2SharpRepositoryClient(
        IOptions<GitRepositoryOptions> options,
        ILogger<LibGit2SharpRepositoryClient> logger,
        IProgress<GitRepositoryProgress> progress)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _progress = progress ?? throw new ArgumentNullException(nameof(progress));
        var workPath = string.IsNullOrWhiteSpace(_options.WorkPath) ? "work" : _options.WorkPath;
        _workPath = Path.IsPathFullyQualified(workPath)
            ? Path.GetFullPath(workPath)
            : Path.GetFullPath(workPath, AppContext.BaseDirectory);
        _repositoryUrl = string.IsNullOrWhiteSpace(_options.RepositoryUrl)
            ? $"https://github.com/{_options.Owner}/{_options.Repository}.git"
            : _options.RepositoryUrl.Trim();
    }

    public Task<string> UpdateAsync(CancellationToken cancellationToken) =>
        UseAsync(repository =>
        {
            UpdateIfChanged(repository);
            return repository.Head.Tip.Sha;
        }, cancellationToken);

    public Task<IReadOnlyList<string>> GetRepositoryFilesAsync(CancellationToken cancellationToken) =>
        UseWorkTreeAsync<IReadOnlyList<string>>(() => Directory
            .EnumerateFiles(_workPath, "*", SearchOption.AllDirectories)
            .Where(path => !IsGitMetadata(path))
            .Select(path => Path.GetRelativePath(_workPath, path).Replace('\\', '/'))
            .ToList(), cancellationToken);

    public Task<byte[]> GetFileAsync(string path, CancellationToken cancellationToken) =>
        UseWorkTreeAsync(() =>
        {
            var fullPath = GetFullPath(path);
            return File.Exists(fullPath) ? File.ReadAllBytes(fullPath) : null;
        }, cancellationToken);

    public Task<StoragePutResult> PutFileAsync(string path, byte[] content, string message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        return UseAsync(repository =>
        {
            UpdateIfChanged(repository);
            var fullPath = GetFullPath(path);
            if (File.Exists(fullPath))
            {
                return File.ReadAllBytes(fullPath).SequenceEqual(content)
                    ? StoragePutResult.AlreadyExists
                    : StoragePutResult.Conflict;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllBytes(fullPath, content);
            CommitAndPush(repository, path, message);
            return StoragePutResult.Success;
        }, cancellationToken);
    }

    public Task DeleteFileAsync(string path, string message, CancellationToken cancellationToken) =>
        ChangeAsync(path, message, _ =>
        {
            var fullPath = GetFullPath(path);
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }, cancellationToken);

    public void Dispose() => _lock.Dispose();

    private void CommitAndPush(Repository repository, string path, string message)
    {
        Commands.Stage(repository, NormalizePath(path));
        var signature = new Signature(
            string.IsNullOrWhiteSpace(_options.AuthorName) ? "BaGetter" : _options.AuthorName,
            string.IsNullOrWhiteSpace(_options.AuthorEmail) ? "bagetter@localhost" : _options.AuthorEmail,
            DateTimeOffset.Now);
        var commit = repository.Commit(message, signature, signature);
        repository.Network.Push(repository.Head, new PushOptions { CredentialsProvider = Credentials });
        _logger.LogInformation("Pushed Git commit {CommitSha}: {CommitMessage}", commit.Sha, message);
    }

    private Task<bool> ChangeAsync(
        string path,
        string message,
        Action<Repository> change,
        CancellationToken cancellationToken) =>
        UseAsync(repository =>
        {
            UpdateIfChanged(repository);
            change(repository);

            if (!repository.RetrieveStatus().IsDirty)
            {
                return true;
            }

            CommitAndPush(repository, path, message);
            return true;
        }, cancellationToken);

    private async Task<T> UseAsync<T>(Func<Repository, T> action, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                EnsureCloned();
                using var repository = new Repository(_workPath);
                return action(repository);
            }, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<T> UseWorkTreeAsync<T>(Func<T> action, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                EnsureCloned();
                return action();
            }, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    private void EnsureCloned()
    {
        if (Repository.IsValid(_workPath))
        {
            return;
        }

        if (Directory.Exists(_workPath) && Directory.EnumerateFileSystemEntries(_workPath).Any())
        {
            throw new InvalidOperationException($"Git work directory '{_workPath}' is not empty");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(_workPath)!);
        var clone = new CloneOptions
        {
            BranchName = string.IsNullOrWhiteSpace(_options.Branch) ? null : _options.Branch.Trim(),
        };
        clone.FetchOptions.CredentialsProvider = Credentials;
        clone.FetchOptions.OnTransferProgress = CreateTransferProgressHandler(
            "Git clone",
            GitRepositoryProgressPhase.Cloning);

        _logger.LogInformation("Cloning {RepositoryUrl} into {WorkPath}", _repositoryUrl, _workPath);
        _progress.Report(new GitRepositoryProgress(GitRepositoryProgressPhase.Cloning));
        var stopwatch = Stopwatch.StartNew();
        Repository.Clone(_repositoryUrl, _workPath, clone);
        _logger.LogInformation(
            "Cloned {RepositoryUrl} into {WorkPath} in {Elapsed}",
            _repositoryUrl,
            _workPath,
            stopwatch.Elapsed);
    }

    private void UpdateIfChanged(Repository repository)
    {
        var remote = repository.Network.Remotes["origin"]
            ?? throw new InvalidOperationException("Git remote 'origin' is missing");
        var branchName = string.IsNullOrWhiteSpace(_options.Branch) ? repository.Head.FriendlyName : _options.Branch.Trim();
        var remoteReference = repository.Network
            .ListReferences(remote, Credentials)
            .SingleOrDefault(reference => string.Equals(
                reference.CanonicalName,
                $"refs/heads/{branchName}",
                StringComparison.Ordinal));

        if (remoteReference == null)
        {
            throw new InvalidOperationException($"Remote branch '{branchName}' does not exist");
        }

        if (string.Equals(repository.Head.Tip?.Sha, remoteReference.TargetIdentifier, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogTrace("Git work directory is current at {CommitSha}", repository.Head.Tip.Sha);
            return;
        }

        var fetchOptions = new FetchOptions
        {
            CredentialsProvider = Credentials,
            OnTransferProgress = CreateTransferProgressHandler(
                "Git fetch",
                GitRepositoryProgressPhase.Fetching),
        };
        _progress.Report(new GitRepositoryProgress(GitRepositoryProgressPhase.Fetching));
        var stopwatch = Stopwatch.StartNew();
        Commands.Fetch(
            repository,
            remote.Name,
            remote.FetchRefSpecs.Select(spec => spec.Specification),
            fetchOptions,
            null);

        var remoteBranch = repository.Branches[$"origin/{branchName}"]
            ?? throw new InvalidOperationException($"Remote branch '{branchName}' does not exist");
        var branch = repository.Branches[branchName] ?? repository.CreateBranch(branchName, remoteBranch.Tip);

        repository.Branches.Update(branch, updater => updater.TrackedBranch = remoteBranch.CanonicalName);
        Commands.Checkout(repository, branch);
        repository.Reset(ResetMode.Hard, remoteBranch.Tip);
        _logger.LogInformation(
            "Updated Git work directory from remote to {CommitSha} in {Elapsed}",
            remoteBranch.Tip.Sha,
            stopwatch.Elapsed);
    }

    private TransferProgressHandler CreateTransferProgressHandler(
        string operation,
        GitRepositoryProgressPhase phase)
    {
        var lastReportedPercent = -5;
        return progress =>
        {
            if (progress.TotalObjects == 0)
            {
                return true;
            }

            var percent = (int)((long)progress.ReceivedObjects * 100 / progress.TotalObjects);
            _progress.Report(new GitRepositoryProgress(
                phase,
                percent,
                progress.ReceivedObjects,
                progress.TotalObjects,
                progress.ReceivedBytes));

            if (percent <= lastReportedPercent ||
                (percent < 100 && percent < lastReportedPercent + 5))
            {
                return true;
            }

            lastReportedPercent = percent;
            _logger.LogInformation(
                "{GitOperation} progress: {ReceivedObjects}/{TotalObjects} objects ({Percent}%), {ReceivedMiB:F1} MiB received",
                operation,
                progress.ReceivedObjects,
                progress.TotalObjects,
                percent,
                progress.ReceivedBytes / 1024d / 1024d);
            return true;
        };
    }

    private Credentials Credentials(
        string _,
        string usernameFromUrl,
        SupportedCredentialTypes supportedCredentialTypes)
    {
        if (!string.IsNullOrWhiteSpace(_options.Token) &&
            supportedCredentialTypes.HasFlag(SupportedCredentialTypes.UsernamePassword))
        {
            var username = usernameFromUrl;
            if (string.IsNullOrWhiteSpace(username))
            {
                username = string.IsNullOrWhiteSpace(_options.Username) ? "x-access-token" : _options.Username;
            }

            return new UsernamePasswordCredentials
            {
                Username = username,
                Password = _options.Token,
            };
        }

        if (supportedCredentialTypes.HasFlag(SupportedCredentialTypes.Default))
        {
            return new DefaultCredentials();
        }

        if (string.IsNullOrWhiteSpace(_options.Token) &&
            supportedCredentialTypes.HasFlag(SupportedCredentialTypes.UsernamePassword))
        {
            throw new InvalidOperationException(
                "The Git remote requires username/password credentials, but no token is configured");
        }

        throw new InvalidOperationException(
            $"The Git remote does not support a configured credential mechanism ({supportedCredentialTypes})");
    }

    private string GetFullPath(string path)
    {
        var normalized = NormalizePath(path);
        var fullPath = Path.GetFullPath(Path.Combine(_workPath, normalized.Replace('/', Path.DirectorySeparatorChar)));
        var root = _workPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Path resolves outside the Git work directory", nameof(path));
        }

        return fullPath;
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path))
        {
            throw new ArgumentException("A relative repository path is required", nameof(path));
        }

        var parts = path.Replace('\\', '/').Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || parts.Any(part => part is "." or ".."))
        {
            throw new ArgumentException("Path resolves outside the Git work directory", nameof(path));
        }

        return string.Join('/', parts);
    }

    private bool IsGitMetadata(string path)
    {
        var gitPath = Path.Combine(_workPath, ".git") + Path.DirectorySeparatorChar;
        return path.StartsWith(gitPath, StringComparison.OrdinalIgnoreCase);
    }
}
