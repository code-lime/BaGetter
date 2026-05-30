using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Octokit;

namespace BaGetter.Git;

public class OctokitGitHubStorageClient : IGitHubStorageClient
{
    private readonly GitRepositoryOptions _options;
    private readonly GitHubClient _client;

    public OctokitGitHubStorageClient(IOptionsSnapshot<GitRepositoryOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value;

        var product = new ProductHeaderValue("BaGetter");
        _client = string.IsNullOrWhiteSpace(_options.ApiBaseUrl)
            ? new GitHubClient(product)
            : new GitHubClient(product, new Uri(_options.ApiBaseUrl));

        if (!string.IsNullOrWhiteSpace(_options.Token))
        {
            _client.Credentials = new Credentials(_options.Token);
        }
    }

    public async Task<string> GetLatestCommitShaAsync(
        string branch,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var resolvedBranch = await ResolveBranchAsync(branch);
        var branchInfo = await _client.Repository.Branch.Get(_options.Owner, _options.Repository, resolvedBranch);

        return branchInfo.Commit.Sha;
    }

    public async Task<IReadOnlyList<string>> GetRepositoryFilesAsync(
        string branch,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var resolvedBranch = await ResolveBranchAsync(branch);
        var branchInfo = await _client.Repository.Branch.Get(_options.Owner, _options.Repository, resolvedBranch);
        var tree = await _client.Git.Tree.GetRecursive(_options.Owner, _options.Repository, branchInfo.Commit.Sha);

        return tree
            .Tree
            .Where(item => item.Type == TreeType.Blob)
            .Select(item => item.Path)
            .ToList();
    }

    public async Task<GitHubStorageContent> GetFileAsync(
        string path,
        string branch,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var contents = string.IsNullOrWhiteSpace(branch)
            ? await _client.Repository.Content.GetAllContents(_options.Owner, _options.Repository, path)
            : await _client.Repository.Content.GetAllContentsByRef(_options.Owner, _options.Repository, path, branch);

        var file = contents.SingleOrDefault();
        if (file == null)
        {
            throw new NotFoundException("GitHub content was not found", HttpStatusCode.NotFound);
        }

        return new GitHubStorageContent(file.Path, file.Sha, file.EncodedContent, file.Encoding, file.DownloadUrl);
    }

    public Task CreateFileAsync(
        string path,
        string message,
        string content,
        string branch,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var request = string.IsNullOrWhiteSpace(branch)
            ? new CreateFileRequest(message, content, convertContentToBase64: false)
            : new CreateFileRequest(message, content, branch, convertContentToBase64: false);

        return _client.Repository.Content.CreateFile(_options.Owner, _options.Repository, path, request);
    }

    public Task DeleteFileAsync(
        string path,
        string message,
        string sha,
        string branch,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var request = string.IsNullOrWhiteSpace(branch)
            ? new DeleteFileRequest(message, sha)
            : new DeleteFileRequest(message, sha, branch);

        return _client.Repository.Content.DeleteFile(_options.Owner, _options.Repository, path, request);
    }

    private async Task<string> ResolveBranchAsync(string branch)
    {
        if (!string.IsNullOrWhiteSpace(branch))
        {
            return branch;
        }

        var repository = await _client.Repository.Get(_options.Owner, _options.Repository);
        return repository.DefaultBranch;
    }
}
