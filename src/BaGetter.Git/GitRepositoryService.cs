using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BaGetter.Core;
using Microsoft.Extensions.Options;
using Octokit;

namespace BaGetter.Git;

public class GitRepositoryService : IStorageService
{
    private const int DefaultCopyBufferSize = 81920;
    private const long MaxGitHubFileSize = 100L * 1024L * 1024L;

    private readonly IGitHubStorageClient _client;
    private readonly string _branch;
    private readonly string _rootPath;
    private readonly string _commitMessagePrefix;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public GitRepositoryService(
        IOptionsSnapshot<GitRepositoryOptions> options,
        IGitHubStorageClient client)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(client);

        _client = client;
        _branch = options.Value.Branch;
        _rootPath = NormalizeRootPath(options.Value.RootPath);
        _commitMessagePrefix = string.IsNullOrWhiteSpace(options.Value.CommitMessagePrefix)
            ? "BaGetter storage"
            : options.Value.CommitMessagePrefix.Trim();
    }

    public async Task DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var gitHubPath = GetGitHubPath(path);

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var existingContent = await GetFileOrNullAsync(gitHubPath, cancellationToken);
            if (existingContent == null)
            {
                return;
            }

            await _client.DeleteFileAsync(
                gitHubPath,
                $"{_commitMessagePrefix}: delete {gitHubPath}",
                existingContent.Sha,
                _branch,
                cancellationToken);
        }
        catch (NotFoundException)
        {
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<Stream> GetAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var content = await GetFileOrNullAsync(GetGitHubPath(path), cancellationToken);
        if (content == null)
        {
            return null;
        }

        return new MemoryStream(DecodeContent(content));
    }

    public async Task<Uri> GetDownloadUriAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var content = await GetFileOrNullAsync(GetGitHubPath(path), cancellationToken);
        if (content == null || string.IsNullOrWhiteSpace(content.DownloadUrl))
        {
            return null;
        }

        return new Uri(content.DownloadUrl);
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

        if (content.CanSeek && content.Length > MaxGitHubFileSize)
        {
            return StoragePutResult.Conflict;
        }

        var gitHubPath = GetGitHubPath(path);
        using var seekableContent = new MemoryStream();
        await content.CopyToAsync(seekableContent, DefaultCopyBufferSize, cancellationToken);

        if (seekableContent.Length > MaxGitHubFileSize)
        {
            return StoragePutResult.Conflict;
        }

        var uploadedBytes = seekableContent.ToArray();

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var existingContent = await GetFileOrNullAsync(gitHubPath, cancellationToken);
            if (existingContent != null)
            {
                return uploadedBytes.SequenceEqual(DecodeContent(existingContent))
                    ? StoragePutResult.AlreadyExists
                    : StoragePutResult.Conflict;
            }

            await _client.CreateFileAsync(
                gitHubPath,
                $"{_commitMessagePrefix}: add {gitHubPath}",
                Convert.ToBase64String(uploadedBytes),
                _branch,
                cancellationToken);

            return StoragePutResult.Success;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task<GitHubStorageContent> GetFileOrNullAsync(
        string path,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _client.GetFileAsync(path, _branch, cancellationToken);
        }
        catch (NotFoundException)
        {
            return null;
        }
    }

    private string GetGitHubPath(string path)
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

    private static byte[] DecodeContent(GitHubStorageContent content)
    {
        if (!string.Equals(content.Encoding, "base64", StringComparison.OrdinalIgnoreCase))
        {
            return Encoding.UTF8.GetBytes(content.Content ?? string.Empty);
        }

        var encodedContent = new string((content.Content ?? string.Empty)
            .Where(c => !char.IsWhiteSpace(c))
            .ToArray());

        return Convert.FromBase64String(encodedContent);
    }
}
