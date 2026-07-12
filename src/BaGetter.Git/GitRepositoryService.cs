using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGetter.Core;
using Microsoft.Extensions.Options;

namespace BaGetter.Git;

public class GitRepositoryService : IStorageService
{
    private const int DefaultCopyBufferSize = 81920;
    private const long MaxGitFileSize = 100L * 1024L * 1024L;

    private readonly IGitRepositoryClient _client;
    private readonly string _rootPath;
    private readonly string _commitMessagePrefix;

    public GitRepositoryService(
        IOptionsSnapshot<GitRepositoryOptions> options,
        IGitRepositoryClient client)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(client);

        _client = client;
        _rootPath = NormalizeRootPath(options.Value.RootPath);
        _commitMessagePrefix = string.IsNullOrWhiteSpace(options.Value.CommitMessagePrefix)
            ? "BaGetter storage"
            : options.Value.CommitMessagePrefix.Trim();
    }

    public async Task DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var repositoryPath = GetRepositoryPath(path);

        await _client.DeleteFileAsync(
            repositoryPath,
            $"{_commitMessagePrefix}: delete {repositoryPath}",
            cancellationToken);
    }

    public async Task<Stream> GetAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var content = await _client.GetFileAsync(GetRepositoryPath(path), cancellationToken);
        if (content == null)
        {
            return null;
        }

        return new MemoryStream(content, writable: false);
    }

    public async Task<Uri> GetDownloadUriAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await Task.CompletedTask;
        GetRepositoryPath(path);
        return null;
    }

    public async Task<StoragePutResult> PutAsync(
        string path,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (string.IsNullOrEmpty(contentType)) throw new ArgumentException("Content type is required", nameof(contentType));

        cancellationToken.ThrowIfCancellationRequested();

        if (content.CanSeek && content.Length > MaxGitFileSize)
        {
            return StoragePutResult.Conflict;
        }

        var repositoryPath = GetRepositoryPath(path);
        using var seekableContent = new MemoryStream();
        await content.CopyToAsync(seekableContent, DefaultCopyBufferSize, cancellationToken);

        if (seekableContent.Length > MaxGitFileSize)
        {
            return StoragePutResult.Conflict;
        }

        var uploadedBytes = seekableContent.ToArray();

        return await _client.PutFileAsync(
            repositoryPath,
            uploadedBytes,
            $"{_commitMessagePrefix}: add {repositoryPath}",
            cancellationToken);
    }

    private string GetRepositoryPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path is required", nameof(path));
        }

        var normalizedPath = path.Replace('\\', '/').Trim('/');
        var segments = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (Path.IsPathRooted(path) ||
            segments.Length == 0 ||
            segments.Any(segment => segment == "." || segment == ".."))
        {
            throw new ArgumentException("Path resolves outside store path", nameof(path));
        }

        return string.IsNullOrEmpty(_rootPath)
            ? normalizedPath
            : $"{_rootPath}/{normalizedPath}";
    }

    private static string NormalizeRootPath(string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            return string.Empty;
        }

        var normalizedPath = rootPath.Replace('\\', '/').Trim('/');
        var segments = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (Path.IsPathRooted(rootPath) ||
            segments.Length == 0 ||
            segments.Any(segment => segment == "." || segment == ".."))
        {
            throw new ArgumentException("Root path resolves outside repository", nameof(rootPath));
        }

        return normalizedPath;
    }

}
