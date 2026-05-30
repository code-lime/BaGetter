using System;
using System.IO;
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

    private readonly IStorageService _storage;
    private readonly IPackageIndexingService _indexer;
    private readonly IConfiguration _configuration;
    private readonly IOptionsSnapshot<GitRepositoryOptions> _options;
    private readonly ILogger<GitRepositoryPackageSynchronizer> _logger;

    public GitRepositoryPackageSynchronizer(
        IStorageService storage,
        IPackageIndexingService indexer,
        IConfiguration configuration,
        IOptionsSnapshot<GitRepositoryOptions> options,
        ILogger<GitRepositoryPackageSynchronizer> logger)
    {
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        _indexer = indexer ?? throw new ArgumentNullException(nameof(indexer));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> TrySyncPackageAsync(
        string id,
        NuGetVersion version,
        CancellationToken cancellationToken)
    {
        if (!_configuration.HasStorageType("GitHub"))
        {
            return false;
        }

        var packagePath = PackagePath(id, version);
        var sourceUrl = BuildSourceUrl(_options.Value);

        try
        {
            await using var packageStream = await _storage.GetAsync(packagePath, cancellationToken);
            if (packageStream == null)
            {
                return false;
            }

            var result = await _indexer.IndexAsync(packageStream, sourceUrl, cancellationToken);
            return result is PackageIndexingResult.Success or PackageIndexingResult.PackageAlreadyExists;
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
}
