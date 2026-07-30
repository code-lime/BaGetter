using BaGetter.Core;
using Xunit;

namespace BaGetter.Tests;

public class ValidateBaGetterOptionsTests
{
    private readonly ValidateBaGetterOptions _validator = new();

    [Theory]
    [InlineData("FileSystem")]
    [InlineData("GitHub")]
    [InlineData("Null")]
    public void AcceptsSupportedStorageProviders(string storageType)
    {
        var result = _validator.Validate(null, CreateOptions(storageType: storageType));

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("MySql")]
    [InlineData("PostgreSql")]
    [InlineData("SqlServer")]
    [InlineData("AzureTable")]
    public void RejectsRemovedDatabaseProviders(string databaseType)
    {
        var result = _validator.Validate(null, CreateOptions(databaseType: databaseType));

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, failure => failure.Contains("Database:Type"));
    }

    [Theory]
    [InlineData("AliyunOss")]
    [InlineData("AwsS3")]
    [InlineData("AzureBlobStorage")]
    [InlineData("GoogleCloud")]
    [InlineData("TencentCos")]
    public void RejectsRemovedStorageProviders(string storageType)
    {
        var result = _validator.Validate(null, CreateOptions(storageType: storageType));

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, failure => failure.Contains("Storage:Type"));
    }

    [Fact]
    public void RejectsRemovedAzureSearchProvider()
    {
        var result = _validator.Validate(null, CreateOptions(searchType: "AzureSearch"));

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, failure => failure.Contains("Search:Type"));
    }

    private static BaGetterOptions CreateOptions(
        string databaseType = "Sqlite",
        string storageType = "GitHub",
        string searchType = "Database")
    {
        return new BaGetterOptions
        {
            Database = new DatabaseOptions { Type = databaseType },
            Storage = new StorageOptions { Type = storageType },
            Search = new SearchOptions { Type = searchType },
            Mirror = new MirrorOptions(),
        };
    }
}
