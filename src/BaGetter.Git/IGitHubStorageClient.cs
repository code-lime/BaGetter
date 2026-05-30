using System.Threading;
using System.Threading.Tasks;

namespace BaGetter.Git;

public interface IGitHubStorageClient
{
    Task<GitHubStorageContent> GetFileAsync(
        string path,
        string branch,
        CancellationToken cancellationToken);

    Task CreateFileAsync(
        string path,
        string message,
        string content,
        string branch,
        CancellationToken cancellationToken);

    Task DeleteFileAsync(
        string path,
        string message,
        string sha,
        string branch,
        CancellationToken cancellationToken);
}
