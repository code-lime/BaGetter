using System.ComponentModel.DataAnnotations;
using BaGetter.Core;

namespace BaGetter.Git;

public class GitRepositoryOptions : StorageOptions
{
    [Required]
    public string Owner { get; set; }

    [Required]
    public string Repository { get; set; }

    public string Token { get; set; }

    public string Branch { get; set; }

    public string RootPath { get; set; }

    public string RepositoryUrl { get; set; }

    public string WorkPath { get; set; } = "work";

    public int UpdateIntervalSeconds { get; set; } = 30;

    public string Username { get; set; } = "x-access-token";

    public string AuthorName { get; set; } = "BaGetter";

    public string AuthorEmail { get; set; } = "bagetter@localhost";

    public string CommitMessagePrefix { get; set; } = "BaGetter storage";
}
