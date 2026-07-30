# BaGetter 🥖🛒

BaGetter is a lightweight [NuGet] and [symbol] server, written in C#.
It's forked from [BaGet] for progressive and community driven development.

<p align="center">
  <img width="100%" src="https://user-images.githubusercontent.com/737941/50140219-d8409700-0258-11e9-94c9-dad24d2b48bb.png">
</p>

![Build status] [![Docker image version]][Docker link] [![Discord][Discord image]][Discord link]

## 🚀 Getting Started

With Docker (SQLite and filesystem storage):

1. `docker run -p 5000:8080 -e Storage__Type=FileSystem -v ./bagetter-data:/data bagetter/bagetter:latest`
2. Browse `http://localhost:5000/` in your browser

With .NET:

1. Install the [.NET 10 SDK]
2. Download and extract [BaGetter's latest release]
3. Start the service with `dotnet BaGetter.dll`
4. Browse `http://localhost:5000/` in your browser

On Windows, you can also run the WPF host from source with
`dotnet run --project src/BaGetter.WPF` and keep BaGetter in the system tray.

GitHub workflows can start an isolated BaGetter server backed by a GitHub repository using the
[composite GitHub Action](action.yml). The action exposes the NuGet service URL and API key as outputs.

With IIS ([official microsoft documentation](https://learn.microsoft.com/aspnet/core/host-and-deploy/iis)):

1. Install the [.NET 10 Hosting Bundle](https://dotnet.microsoft.com/download/dotnet/10.0)
2. Download the [zip release](https://github.com/bagetter/BaGetter/releases) of BaGetter
3. Unpack the zip file contents to a folder of your choice
4. Create a new or configure an existing IIS site to point its physical path to the folder where you unpacked the zip file

For more information, please refer to the [documentation].

## 📦 Features

* **Cross-platform web host**: runs on Windows, macOS, and Linux, with an additional WPF host for Windows.
* **ARM** (64bit) **support**. You can host your NuGets on a device like Raspberry Pi!
* **Focused persistence**: stores metadata in SQLite and packages in a GitHub repository, the local filesystem, or the null storage provider.
* **Automation ready**: supports [Docker][Docker doc link] and a composite GitHub Action.
* **Offline support**: [Mirror a NuGet server][Read through caching] to speed up builds and enable offline downloads

## 🤝 Contributing

We welcome contributions! Check out the [Contributing Guide](CONTRIBUTING.md) to get started.

## 📄 License

This project is licensed under the [MIT License](LICENSE).

## 📞 Contact

If you have questions, feel free to open an [issue] or join our [Discord Server][Discord link] for discussions.

## 🤝🏼 Contributors

Thanks to everyone who helps to make BaGetter better!

<a href="https://github.com/bagetter/BaGetter/graphs/contributors">
  <img src="https://contrib.rocks/image?repo=bagetter/BaGetter" />
</a>


[Build status]: https://img.shields.io/github/actions/workflow/status/bagetter/BaGetter/.github/workflows/main.yml?logo=github&logoColor=fff

[Docker image version]: https://img.shields.io/docker/v/bagetter/bagetter?logo=docker&logoColor=fff&label=version
[Docker link]: https://hub.docker.com/r/bagetter/bagetter
[Docker doc link]: https://www.bagetter.com/docs/Installation/docker

[Discord image]: https://img.shields.io/discord/1181167608427450388?logo=discord&logoColor=fff
[Discord link]: https://discord.gg/XsAmm6f2hZ

[NuGet]: https://learn.microsoft.com/nuget/what-is-nuget
[symbol]: https://learn.microsoft.com/windows-hardware/drivers/debugger/symbol-stores-and-symbol-servers
[.NET 10 SDK]: https://dotnet.microsoft.com/download/dotnet/10.0
[Issue]: https://github.com/bagetter/BaGetter/issues
[BaGet]: https://github.com/loic-sharma/BaGet

[BaGetter's latest release]: https://github.com/bagetter/BaGetter/releases

[Documentation]: https://www.bagetter.com/
[Read through caching]: https://www.bagetter.com/docs/configuration#enable-read-through-caching
