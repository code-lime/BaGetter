using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGetter.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Versioning;

namespace BaGetter.Git;

public class GitRepositoryPackageSynchronizer : IPackageStorageSynchronizer
{
    private const string PackagesPathPrefix = "packages";
    private static readonly ConcurrentDictionary<string, RepositorySyncState> SyncStates = new(StringComparer.OrdinalIgnoreCase);

    private readonly IGitHubStorageClient _client;
    private readonly IStorageService _storage;
    private readonly IPackageDatabase _db;
    private readonly IPackageIndexingService _indexer;
    private readonly IConfiguration _configuration;
    private readonly IOptionsSnapshot<GitRepositoryOptions> _options;
    private readonly ILogger<GitRepositoryPackageSynchronizer> _logger;

    public GitRepositoryPackageSynchronizer(
        IGitHubStorageClient client,
        IStorageService storage,
        IPackageDatabase db,
        IPackageIndexingService indexer,
        IConfiguration configuration,
        IOptionsSnapshot<GitRepositoryOptions> options,
        ILogger<GitRepositoryPackageSynchronizer> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _indexer = indexer ?? throw new ArgumentNullException(nameof(indexer));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> TrySyncAsync(CancellationToken cancellationToken)
    {
        if (!IsGitHubStorage())
        {
            return false;
        }

        try
        {
            await EnsureRepositoryIsCurrentAsync(cancellationToken);
            return true;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to synchronize packages from GitHub repository");
            return false;
        }
    }

    public async Task<bool> TrySyncPackageListAsync(
        string id,
        CancellationToken cancellationToken)
    {
        if (!IsGitHubStorage())
        {
            return false;
        }

        try
        {
            var state = await EnsureRepositoryIsCurrentAsync(cancellationToken);
            await RemoveMissingVersionsAsync(id, state, cancellationToken);

            return true;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to synchronize package list {PackageId} from GitHub repository", id);
            return false;
        }
    }

    public async Task<bool> TrySyncPackageAsync(
        string id,
        NuGetVersion version,
        CancellationToken cancellationToken)
    {
        if (!IsGitHubStorage())
        {
            return false;
        }

        await EnsureRepositoryIsCurrentAsync(cancellationToken);

        var packagePath = PackagePath(id, version);

        try
        {
            return await IndexPackagePathAsync(packagePath, cancellationToken);
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }
        catch (Exception e)
        {
            _logger.LogError(
                e,
                "Failed to synchronize package {PackageId} {PackageVersion} from storage path {PackagePath}",
                id,
                version,
                packagePath);

            return false;
        }
    }

    private async Task<RepositorySyncState> EnsureRepositoryIsCurrentAsync(CancellationToken cancellationToken)
    {
        var options = _options.Value;
        var state = SyncStates.GetOrAdd(BuildStateKey(options), _ => new RepositorySyncState());
        var latestCommitSha = await _client.GetLatestCommitShaAsync(options.Branch, cancellationToken);

        if (string.Equals(state.LastCommitSha, latestCommitSha, StringComparison.OrdinalIgnoreCase))
        {
            return state;
        }

        await state.Lock.WaitAsync(cancellationToken);
        try
        {
            latestCommitSha = await _client.GetLatestCommitShaAsync(options.Branch, cancellationToken);
            if (string.Equals(state.LastCommitSha, latestCommitSha, StringComparison.OrdinalIgnoreCase))
            {
                return state;
            }

            _logger.LogInformation(
                "GitHub repository commit changed from {PreviousCommitSha} to {LatestCommitSha}. Rebuilding package metadata...",
                state.LastCommitSha ?? "<none>",
                latestCommitSha);

            var packageFiles = await GetPackageFilesAsync(options, cancellationToken);

            foreach (var removedPackage in state.PackageFiles.Values.Where(p => !packageFiles.ContainsKey(p.StoragePath)))
            {
                await _db.HardDeletePackageAsync(removedPackage.Id, removedPackage.Version, cancellationToken);
            }

            foreach (var packageFile in packageFiles.Values)
            {
                await IndexPackagePathAsync(packageFile.StoragePath, cancellationToken);
            }

            state.PackageFiles = packageFiles;
            state.LastCommitSha = await _client.GetLatestCommitShaAsync(options.Branch, cancellationToken);

            _logger.LogInformation(
                "Finished rebuilding package metadata from GitHub repository at commit {CommitSha}. Indexed {PackageCount} package files.",
                state.LastCommitSha,
                state.PackageFiles.Count);

            return state;
        }
        finally
        {
            state.Lock.Release();
        }
    }

    private async Task<IReadOnlyDictionary<string, PackageFile>> GetPackageFilesAsync(
        GitRepositoryOptions options,
        CancellationToken cancellationToken)
    {
        var rootPath = NormalizeRootPath(options.RootPath);
        var repositoryFiles = await _client.GetRepositoryFilesAsync(options.Branch, cancellationToken);
        var packageFiles = new Dictionary<string, PackageFile>(StringComparer.OrdinalIgnoreCase);

        foreach (var repositoryPath in repositoryFiles)
        {
            if (!TryGetStoragePath(repositoryPath, rootPath, out var storagePath))
            {
                continue;
            }

            if (!TryParsePackagePath(storagePath, out var packageFile))
            {
                continue;
            }

            packageFiles[packageFile.StoragePath] = packageFile;
        }

        return packageFiles;
    }

    private async Task RemoveMissingVersionsAsync(
        string id,
        RepositorySyncState state,
        CancellationToken cancellationToken)
    {
        var repositoryVersions = state
            .PackageFiles
            .Values
            .Where(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase))
            .Select(p => p.Version.ToNormalizedString())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var localPackages = await _db.FindAsync(id, includeUnlisted: true, cancellationToken);
        foreach (var localPackage in localPackages)
        {
            if (!repositoryVersions.Contains(localPackage.Version.ToNormalizedString()))
            {
                await _db.HardDeletePackageAsync(localPackage.Id, localPackage.Version, cancellationToken);
            }
        }
    }

    private async Task<bool> IndexPackagePathAsync(
        string packagePath,
        CancellationToken cancellationToken)
    {
        await using var packageStream = await _storage.GetAsync(packagePath, cancellationToken);
        if (packageStream == null)
        {
            return false;
        }

        var result = await _indexer.IndexAsync(packageStream, BuildSourceUrl(_options.Value), cancellationToken);
        return result is PackageIndexingResult.Success or PackageIndexingResult.PackageAlreadyExists;
    }

    private bool IsGitHubStorage()
    {
        return _configuration.HasStorageType("GitHub");
    }

    private static string PackagePath(string id, NuGetVersion version)
    {
        var lowercasedId = id.ToLowerInvariant();
        var lowercasedNormalizedVersion = version.ToNormalizedString().ToLowerInvariant();

        return Path.Combine(
            PackagesPathPrefix,
            lowercasedId,
            lowercasedNormalizedVersion,
            $"{lowercasedId}.{lowercasedNormalizedVersion}.nupkg");
    }

    private static string BuildSourceUrl(GitRepositoryOptions options)
    {
        var branch = string.IsNullOrWhiteSpace(options.Branch) ? string.Empty : $"#{options.Branch}";
        return $"github://{options.Owner}/{options.Repository}{branch}";
    }

    private static string BuildStateKey(GitRepositoryOptions options)
    {
        return string.Join(
            "|",
            options.Owner?.Trim() ?? string.Empty,
            options.Repository?.Trim() ?? string.Empty,
            options.Branch?.Trim() ?? string.Empty,
            NormalizeRootPath(options.RootPath));
    }

    private static bool TryGetStoragePath(
        string repositoryPath,
        string rootPath,
        out string storagePath)
    {
        storagePath = null;

        if (string.IsNullOrWhiteSpace(repositoryPath))
        {
            return false;
        }

        var normalizedPath = repositoryPath.Replace('\\', '/').Trim('/');

        if (!string.IsNullOrEmpty(rootPath))
        {
            if (!normalizedPath.StartsWith(rootPath + "/", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            normalizedPath = normalizedPath[(rootPath.Length + 1)..];
        }

        storagePath = normalizedPath;
        return true;
    }

    private static bool TryParsePackagePath(
        string storagePath,
        out PackageFile packageFile)
    {
        packageFile = null;

        if (string.IsNullOrWhiteSpace(storagePath) ||
            storagePath.EndsWith(".symbols.nupkg", StringComparison.OrdinalIgnoreCase) ||
            storagePath.EndsWith(".snupkg", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var segments = storagePath.Replace('\\', '/').Trim('/').Split('/');
        if (segments.Length != 4 ||
            !string.Equals(segments[0], PackagesPathPrefix, StringComparison.OrdinalIgnoreCase) ||
            !segments[3].EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase) ||
            !NuGetVersion.TryParse(segments[2], out var version))
        {
            return false;
        }

        packageFile = new PackageFile(storagePath, segments[1], version);
        return true;
    }

    private static string NormalizeRootPath(string rootPath)
    {
        return string.IsNullOrWhiteSpace(rootPath)
            ? string.Empty
            : rootPath.Replace('\\', '/').Trim('/');
    }

    private sealed class RepositorySyncState
    {
        public readonly SemaphoreSlim Lock = new(1, 1);

        public string LastCommitSha { get; set; }

        public IReadOnlyDictionary<string, PackageFile> PackageFiles { get; set; } =
            new Dictionary<string, PackageFile>(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class PackageFile
    {
        public PackageFile(
            string storagePath,
            string id,
            NuGetVersion version)
        {
            StoragePath = storagePath;
            Id = id;
            Version = version;
        }

        public string StoragePath { get; }

        public string Id { get; }

        public NuGetVersion Version { get; }
    }
}
