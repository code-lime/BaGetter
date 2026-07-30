# Run BaGetter on your computer

## Run the web host

1. Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
2. Download and extract [BaGetter's latest release](https://github.com/bagetter/BaGetter/releases)
3. Start the service with `dotnet BaGetter.dll`
4. Browse `http://localhost:5000/` in your browser

To run directly from a source checkout, use `dotnet run --project src/BaGetter` from the repository root.

## Run the Windows WPF host

On Windows, run `dotnet run --project src/BaGetter.WPF` from a source checkout. The WPF host starts the same web server and keeps it available from the system tray.

## Configure BaGetter

You can modify BaGetter's configurations by editing the `appsettings.json` file. For the full list of configurations, please refer to [BaGetter's configuration](../configuration.md) guide.

## Publish packages

Publish your first package with:

```shell
dotnet nuget push -s http://localhost:5000/v3/index.json package.1.0.0.nupkg
```

Publish your first [symbol package](https://learn.microsoft.com/nuget/create-packages/symbol-packages-snupkg) with:

```shell
dotnet nuget push -s http://localhost:5000/v3/index.json symbol.package.1.0.0.snupkg
```

:::warning

You should secure your server by requiring an API Key to publish packages. For more information, please refer to the [Require an API Key](../configuration.md#require-an-api-key) guide.

:::

## Restore packages

You can restore packages by using the following package source:

`http://localhost:5000/v3/index.json`

Some helpful guides:

- [Visual Studio](https://learn.microsoft.com/nuget/consume-packages/install-use-packages-visual-studio#package-sources)
- [NuGet.config](https://learn.microsoft.com/nuget/reference/nuget-config-file#package-source-sections)

## Symbol server

You can load symbols by using the following symbol location:

`http://localhost:5000/api/download/symbols`

For Visual Studio, please refer to the [Configure Debugging](https://learn.microsoft.com/visualstudio/debugger/specify-symbol-dot-pdb-and-source-files-in-the-visual-studio-debugger#configure-location-of-symbol-files-and-loading-options) guide.
