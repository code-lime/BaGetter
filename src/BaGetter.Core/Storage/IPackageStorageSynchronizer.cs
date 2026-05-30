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
    /// Attempt to index a package from the configured storage provider.
    /// </summary>
    /// <returns>Whether the package exists or was indexed successfully.</returns>
    Task<bool> TrySyncPackageAsync(
        string id,
        NuGetVersion version,
        CancellationToken cancellationToken);
}
