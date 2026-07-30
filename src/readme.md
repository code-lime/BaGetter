# BaGetter Source Code

These folders contain the core components of BaGetter:

* `BaGetter` - The app's entry point that glues everything together.
* `BaGetter.Core` - BaGetter's core logic and services.
* `BaGetter.Web` - The [NuGet server APIs](https://learn.microsoft.com/nuget/api/overview) and web UI.
* `BaGetter.WPF` - The Windows system-tray host for BaGetter.
* `BaGetter.Protocol` - Libraries to interact with [NuGet servers' APIs](https://learn.microsoft.com/nuget/api/overview).
* `BaGetter.Git` - The GitHub repository storage provider and synchronizer.

BaGetter stores package metadata in SQLite:

* `BaGetter.Database.Sqlite` - BaGetter's SQLite database provider.

Filesystem and null storage implementations are part of `BaGetter.Core`.

