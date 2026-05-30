using NuGet.Packaging;
using Octokit;

internal static class GitHubPackageUploader
{
    private const long MaxGitHubFileSize = 100L * 1024L * 1024L;

    public static async Task UploadAsync(ActionInputs inputs, CancellationToken cancellationToken)
    {
        if (!File.Exists(inputs.PackageFile))
        {
            throw new FileNotFoundException("Package file was not found", inputs.PackageFile);
        }

        var packageBytes = await File.ReadAllBytesAsync(inputs.PackageFile, cancellationToken);
        if (packageBytes.Length > MaxGitHubFileSize)
        {
            throw new InvalidOperationException("GitHub repository contents API does not support files over 100 MiB");
        }

        await using var packageStream = new MemoryStream(packageBytes, writable: false);
        using var packageReader = new PackageArchiveReader(packageStream, leaveStreamOpen: true);
        var identity = await packageReader.GetIdentityAsync(cancellationToken);

        var packageId = identity.Id.ToLowerInvariant();
        var packageVersion = identity.Version.ToNormalizedString().ToLowerInvariant();
        var packagePath = BuildPackagePath(inputs.RootPath, packageId, packageVersion);
        var content = Convert.ToBase64String(packageBytes);

        var client = CreateClient(inputs);
        var existingContent = await GetExistingContentOrNullAsync(client, inputs, packagePath);
        var message = $"{inputs.CommitMessagePrefix}: upload {packageId} {packageVersion}";

        if (existingContent == null)
        {
            var request = string.IsNullOrWhiteSpace(inputs.Branch)
                ? new CreateFileRequest(message, content, convertContentToBase64: false)
                : new CreateFileRequest(message, content, inputs.Branch, convertContentToBase64: false);

            await client.Repository.Content.CreateFile(inputs.Owner, inputs.Repository, packagePath, request);
            Console.WriteLine($"Uploaded {inputs.PackageFile} to {packagePath}");
            return;
        }

        if (!inputs.Overwrite)
        {
            Console.WriteLine($"Package already exists at {packagePath}; set overwrite to true to replace it");
            return;
        }

        var updateRequest = string.IsNullOrWhiteSpace(inputs.Branch)
            ? new UpdateFileRequest(message, content, existingContent.Sha, convertContentToBase64: false)
            : new UpdateFileRequest(message, content, existingContent.Sha, inputs.Branch, convertContentToBase64: false);

        await client.Repository.Content.UpdateFile(inputs.Owner, inputs.Repository, packagePath, updateRequest);
        Console.WriteLine($"Updated {inputs.PackageFile} at {packagePath}");
    }

    private static GitHubClient CreateClient(ActionInputs inputs)
    {
        var product = new ProductHeaderValue("BaGetter-GitHubAction");
        var client = string.IsNullOrWhiteSpace(inputs.ApiBaseUrl)
            ? new GitHubClient(product)
            : new GitHubClient(product, new Uri(inputs.ApiBaseUrl));

        client.Credentials = new Credentials(inputs.Token);
        return client;
    }

    private static async Task<RepositoryContent?> GetExistingContentOrNullAsync(
        GitHubClient client,
        ActionInputs inputs,
        string path)
    {
        try
        {
            var contents = string.IsNullOrWhiteSpace(inputs.Branch)
                ? await client.Repository.Content.GetAllContents(inputs.Owner, inputs.Repository, path)
                : await client.Repository.Content.GetAllContentsByRef(inputs.Owner, inputs.Repository, path, inputs.Branch);

            return contents.SingleOrDefault();
        }
        catch (NotFoundException)
        {
            return null;
        }
    }

    private static string BuildPackagePath(string rootPath, string packageId, string packageVersion)
    {
        var prefix = NormalizePath(rootPath);
        var path = $"packages/{packageId}/{packageVersion}/{packageId}.{packageVersion}.nupkg";

        return string.IsNullOrEmpty(prefix) ? path : $"{prefix}/{path}";
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var normalized = path.Replace('\\', '/').Trim('/');
        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (Path.IsPathRooted(path) ||
            segments.Any(segment => segment == "." || segment == ".."))
        {
            throw new ArgumentException("Root path resolves outside repository", nameof(path));
        }

        return normalized;
    }
}
