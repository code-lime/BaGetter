using System.ComponentModel.DataAnnotations;
using BaGetter.Core;

namespace BaGetter.Git;

public class GitRepositoryOptions : StorageOptions
{
    [Required]
    public string Owner { get; set; }

    [Required]
    public string Repository { get; set; }

    [Required]
    public string Token { get; set; }

    public string Branch { get; set; }

    public string RootPath { get; set; }

    public string ApiBaseUrl { get; set; } = "https://api.github.com";

    public string CommitMessagePrefix { get; set; } = "BaGetter storage";
}
