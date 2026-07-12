using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BaGetter.Core;

namespace BaGetter.Git;

public interface IGitRepositoryClient
{
    Task<string> UpdateAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> GetRepositoryFilesAsync(CancellationToken cancellationToken);

    Task<byte[]> GetFileAsync(string path, CancellationToken cancellationToken);

    Task<StoragePutResult> PutFileAsync(
        string path,
        byte[] content,
        string message,
        CancellationToken cancellationToken);

    Task DeleteFileAsync(
        string path,
        string message,
        CancellationToken cancellationToken);
}
