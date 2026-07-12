using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BaGetter.Core;
using BaGetter.Git;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace BaGetter.Tests;

public class GitRepositoryServiceTests
{
    public class GetAsync : FactsBase
    {
        [Fact]
        public async Task GetsStream()
        {
            _client.Files["packages/test.nupkg"] = Content("Hello world");

            var result = await _target.GetAsync("test.nupkg");

            Assert.Equal("Hello world", await ToStringAsync(result));
        }

        [Fact]
        public async Task ReturnsNullIfMissing()
        {
            var result = await _target.GetAsync("test.nupkg");

            Assert.Null(result);
        }

        [Fact]
        public async Task RejectsPathsOutsideRoot()
        {
            foreach (var path in OutsideStorePathData)
            {
                await Assert.ThrowsAsync<ArgumentException>(() => _target.GetAsync(path));
            }
        }
    }

    public class GetDownloadUriAsync : FactsBase
    {
        [Fact]
        public async Task GetsDownloadUri()
        {
            _client.Files["packages/test.nupkg"] = Content("Hello world");

            var result = await _target.GetDownloadUriAsync("test.nupkg");

            Assert.Null(result);
        }

        [Fact]
        public async Task ReturnsNullIfMissing()
        {
            var result = await _target.GetDownloadUriAsync("test.nupkg");

            Assert.Null(result);
        }
    }

    public class PutAsync : FactsBase
    {
        [Fact]
        public async Task CreatesFile()
        {
            using var content = BytesStream(0, 1, 2, 255);

            var result = await _target.PutAsync("test.nupkg", content, "application/octet-stream");

            Assert.Equal(StoragePutResult.Success, result);
            Assert.Equal("packages/test.nupkg", _client.CreatedPath);
            Assert.Equal("BaGetter storage: add packages/test.nupkg", _client.CreatedMessage);
            Assert.Equal(new byte[] { 0, 1, 2, 255 }, _client.CreatedContent);
        }

        [Fact]
        public async Task ReturnsAlreadyExistsIfContentMatches()
        {
            _client.Files["packages/test.nupkg"] = Content(new byte[] { 0, 1, 2, 255 });
            using var content = BytesStream(0, 1, 2, 255);

            var result = await _target.PutAsync("test.nupkg", content, "application/octet-stream");

            Assert.Equal(StoragePutResult.AlreadyExists, result);
            Assert.Null(_client.CreatedPath);
        }

        [Fact]
        public async Task ReturnsConflictIfContentDiffers()
        {
            _client.Files["packages/test.nupkg"] = Content("Hello world");
            using var content = StringStream("Different content");

            var result = await _target.PutAsync("test.nupkg", content, "application/octet-stream");

            Assert.Equal(StoragePutResult.Conflict, result);
            Assert.Null(_client.CreatedPath);
        }

        [Fact]
        public async Task ReturnsConflictIfContentIsTooLarge()
        {
            using var content = new MemoryStream();
            content.SetLength((100L * 1024L * 1024L) + 1L);

            var result = await _target.PutAsync("test.nupkg", content, "application/octet-stream");

            Assert.Equal(StoragePutResult.Conflict, result);
            Assert.Null(_client.CreatedPath);
        }
    }

    public class DeleteAsync : FactsBase
    {
        [Fact]
        public async Task DoesNotThrowIfMissing()
        {
            await _target.DeleteAsync("test.nupkg");

            Assert.Null(_client.DeletedPath);
        }

        [Fact]
        public async Task DeletesExistingFile()
        {
            _client.Files["packages/test.nupkg"] = Content("Hello world");

            await _target.DeleteAsync("test.nupkg");

            Assert.Equal("packages/test.nupkg", _client.DeletedPath);
            Assert.Equal("BaGetter storage: delete packages/test.nupkg", _client.DeletedMessage);
        }
    }

    public class FactsBase
    {
        protected readonly FakeGitHubStorageClient _client;
        protected readonly GitRepositoryService _target;

        public FactsBase()
        {
            _client = new FakeGitHubStorageClient();

            var options = new Mock<IOptionsSnapshot<GitRepositoryOptions>>();
            options
                .Setup(o => o.Value)
                .Returns(new GitRepositoryOptions
                {
                    Owner = "org",
                    Repository = "repo",
                    Token = "token",
                    Branch = "main",
                    RootPath = "packages",
                });

            _target = new GitRepositoryService(options.Object, _client);
        }

        protected IEnumerable<string> OutsideStorePathData
        {
            get
            {
                yield return "../file";
                yield return ".";
                yield return "hello/../file";
                yield return "/absolute";
                yield return "";
            }
        }

        protected static byte[] Content(string content)
        {
            return Encoding.UTF8.GetBytes(content);
        }

        protected static byte[] Content(byte[] content)
        {
            return content;
        }

        protected static Stream StringStream(string input)
        {
            return new MemoryStream(Encoding.UTF8.GetBytes(input));
        }

        protected static Stream BytesStream(params byte[] input)
        {
            return new MemoryStream(input);
        }

        protected static async Task<string> ToStringAsync(Stream input)
        {
            using var reader = new StreamReader(input);
            return await reader.ReadToEndAsync();
        }
    }

    public class FakeGitHubStorageClient : IGitRepositoryClient
    {
        public readonly Dictionary<string, byte[]> Files = new(StringComparer.Ordinal);

        public string CreatedPath { get; private set; }

        public string CreatedMessage { get; private set; }

        public byte[] CreatedContent { get; private set; }

        public string DeletedPath { get; private set; }

        public string DeletedMessage { get; private set; }

        public Task<string> UpdateAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult("sha");
        }

        public Task<IReadOnlyList<string>> GetRepositoryFilesAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult((IReadOnlyList<string>)Files.Keys.ToList());
        }

        public Task<byte[]> GetFileAsync(
            string path,
            CancellationToken cancellationToken)
        {
            if (!Files.TryGetValue(path, out var content))
            {
                return Task.FromResult<byte[]>(null);
            }

            return Task.FromResult(content);
        }

        public Task<StoragePutResult> PutFileAsync(
            string path,
            byte[] content,
            string message,
            CancellationToken cancellationToken)
        {
            if (Files.TryGetValue(path, out var existing))
            {
                return Task.FromResult(existing.SequenceEqual(content)
                    ? StoragePutResult.AlreadyExists
                    : StoragePutResult.Conflict);
            }

            CreatedPath = path;
            CreatedMessage = message;
            CreatedContent = content;
            return Task.FromResult(StoragePutResult.Success);
        }

        public Task DeleteFileAsync(
            string path,
            string message,
            CancellationToken cancellationToken)
        {
            if (Files.ContainsKey(path))
            {
                DeletedPath = path;
                DeletedMessage = message;
            }
            return Task.CompletedTask;
        }
    }
}
