using System.Threading;
using System.Threading.Tasks;
using NuGet.Versioning;

namespace BaGetter.Core;

/// <summary>
/// Syncs package metadata from a storage provider when the database is missing an entry.
/// </summary>
public interface IPackageStorageSynchronizer
{
    /// <summary>
    /// Attempt to refresh the local package metadata from the configured storage provider.
    /// </summary>
    /// <returns>Whether the refresh completed successfully.</returns>
    Task<bool> TrySyncAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Attempt to refresh all versions of a package from the configured storage provider.
    /// </summary>
    /// <returns>Whether the package list was refreshed successfully.</returns>
    Task<bool> TrySyncPackageListAsync(
        string id,
        CancellationToken cancellationToken);

    /// <summary>
    /// Attempt to index a package from the configured storage provider.
    /// </summary>
    /// <returns>Whether the package exists or was indexed successfully.</returns>
    Task<bool> TrySyncPackageAsync(
        string id,
        NuGetVersion version,
        CancellationToken cancellationToken);
}
