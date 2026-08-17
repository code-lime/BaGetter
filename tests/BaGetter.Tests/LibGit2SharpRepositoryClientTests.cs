using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BaGetter.Git;
using LibGit2Sharp;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace BaGetter.Tests;

public class LibGit2SharpRepositoryClientTests
{
    private const string Token = "installation-token";

    [Theory]
    [InlineData(null, null, "x-access-token")]
    [InlineData("configured-user", null, "configured-user")]
    [InlineData("configured-user", "url-user", "url-user")]
    public async Task UpdateAsyncUsesBasicCredentials(
        string configuredUsername,
        string usernameFromUrl,
        string expectedUsername)
    {
        var usernameOnlyAuthorization = string.IsNullOrWhiteSpace(usernameFromUrl)
            ? null
            : $"Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes($"{usernameFromUrl}:"))}";
        var authorization = new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        builder.Logging.ClearProviders();
        await using var server = builder.Build();
        server.Run(context =>
        {
            var value = context.Request.Headers.Authorization.ToString();
            if (string.IsNullOrWhiteSpace(value) ||
                string.Equals(value, usernameOnlyAuthorization, StringComparison.Ordinal))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.Headers.WWWAuthenticate = "Basic realm=\"BaGetter test\"";
            }
            else
            {
                authorization.TrySetResult(value);
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
            }

            return Task.CompletedTask;
        });
        await server.StartAsync();

        var address = server.Services
            .GetRequiredService<IServer>()
            .Features
            .Get<IServerAddressesFeature>()!
            .Addresses
            .Single();
        var repositoryUrl = new UriBuilder(address) { Path = "repository.git" };
        if (!string.IsNullOrWhiteSpace(usernameFromUrl))
        {
            repositoryUrl.UserName = usernameFromUrl;
        }

        var workPath = Path.Combine(Path.GetTempPath(), $"bagetter-auth-{Guid.NewGuid():N}");
        var options = new GitRepositoryOptions
        {
            Owner = "unused",
            Repository = "unused",
            Token = Token,
            RepositoryUrl = repositoryUrl.Uri.AbsoluteUri,
            WorkPath = workPath,
        };
        if (configuredUsername != null)
        {
            options.Username = configuredUsername;
        }

        using var target = new LibGit2SharpRepositoryClient(
            Options.Create(options),
            NullLogger<LibGit2SharpRepositoryClient>.Instance,
            new GitRepositoryStatus());
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        try
        {
            var update = target.UpdateAsync(timeout.Token);
            var observedAuthorization = await authorization.Task.WaitAsync(timeout.Token);

            await Assert.ThrowsAnyAsync<LibGit2SharpException>(() => update);
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{expectedUsername}:{Token}"));
            Assert.Equal($"Basic {credentials}", observedAuthorization);
        }
        finally
        {
            if (Directory.Exists(workPath))
            {
                Directory.Delete(workPath, recursive: true);
            }
        }
    }
}
