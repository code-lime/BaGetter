using CommandLine;

internal sealed class ActionInputs
{
    [Option("package-file", Required = true, HelpText = "Path to the .nupkg file to upload")]
    public required string PackageFile { get; init; }

    [Option("owner", Required = true, HelpText = "GitHub repository owner")]
    public required string Owner { get; init; }

    [Option("repository", Required = true, HelpText = "GitHub repository name")]
    public required string Repository { get; init; }

    [Option("token", Required = true, HelpText = "GitHub token with repository contents write access")]
    public required string Token { get; init; }

    [Option("branch", Required = false, HelpText = "Target branch. Empty means the repository default branch")]
    public string? Branch { get; init; }

    [Option("root-path", Required = false, HelpText = "Optional BaGetter storage root path inside the repository")]
    public string RootPath { get; init; } = "";

    [Option("api-base-url", Required = false, Default = "https://api.github.com", HelpText = "GitHub API base URL")]
    public string ApiBaseUrl { get; init; } = "https://api.github.com";

    [Option("commit-message-prefix", Required = false, Default = "BaGetter package upload", HelpText = "Commit message prefix")]
    public string CommitMessagePrefix { get; init; } = "BaGetter package upload";

    [Option("overwrite", Required = false, Default = false, HelpText = "Replace an existing package file")]
    public bool Overwrite { get; init; }
}
