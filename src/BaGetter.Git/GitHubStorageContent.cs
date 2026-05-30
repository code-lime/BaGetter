namespace BaGetter.Git;

public class GitHubStorageContent
{
    public GitHubStorageContent(
        string path,
        string sha,
        string content,
        string encoding,
        string downloadUrl)
    {
        Path = path;
        Sha = sha;
        Content = content;
        Encoding = encoding;
        DownloadUrl = downloadUrl;
    }

    public string Path { get; }

    public string Sha { get; }

    public string Content { get; }

    public string Encoding { get; }

    public string DownloadUrl { get; }
}
