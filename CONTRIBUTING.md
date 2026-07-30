## Contribute

Please read the [code of conduct] before contributing.

If you want to run from BaGetter's source code:

1. Install the [.NET 10 SDK] and [Node.js]
2. Run `git clone https://github.com/bagetter/BaGetter.git`
3. Navigate to `./BaGetter`
4. Start the web host with `dotnet run --project src/BaGetter`
5. Open the URL `http://localhost:50561/v3/index.json` in your browser

On Windows, use `dotnet run --project src/BaGetter.WPF` to start the WPF system-tray host.
Run `dotnet test` from the repository root before submitting a change.

[Code of conduct]: CODE_OF_CONDUCT.md
[.NET 10 SDK]: https://dotnet.microsoft.com/download/dotnet/10.0
[Node.js]: https://nodejs.org/
