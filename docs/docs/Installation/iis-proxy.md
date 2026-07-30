# Windows IIS Proxy

With a few extra steps you can run BaGetter behind a Windows IIS proxy. This has many benefits, including automatic restarts on reboots.

## IIS setup

1. Install the [.NET 10 Hosting Bundle](https://dotnet.microsoft.com/download/dotnet/10.0) on the web server.
2. Copy the BaGetter directory over to your hosting area such as `C:\Inetpub\wwwroot\BaGetter`
3. Using IIS Manager, create a new Application Pool:
    - Name = `BaGetterAppPool` (can be whatever you want)
    - .NET CLR version = No Managed Code
    - Managed Pipeline Mode = Integrated
    - Start application pool immediately = checked
4. Using IIS Manager, create a new web site:
    - Choose your site name and physical path
    - Choose `BaGetterAppPool` as the application pool
    - In the Binding area, enter the default BaGetter port of 5000

## BaGetter folder permissions

You **may** need to give special permissions to the top-level BaGetter folder so that the app can persist its state. This is necessary as the Application Pools' identity is a virtual account that isn't recognized by the Windows User Management Console. For more information, please refer to ASP.NET Core's documentation:

- [Application Pools](https://learn.microsoft.com/aspnet/core/host-and-deploy/iis/#application-pools)
- [Application Pool Identity](https://learn.microsoft.com/aspnet/core/host-and-deploy/iis/#application-pool-identity)

## Alternative storage path

:::info

Virtual Directories do not work with IIS and Kestrel.
For more information, please refer to [ASP.NET Core's documentation](https://learn.microsoft.com/aspnet/core/host-and-deploy/iis/#virtual-directories).

:::

Ensure that backslashes in the storage `Path` are escaped for JSON:

```json
...
  "Storage": {
    "Type": "FileSystem",
    "Path": "C:\\AnotherFolder\\Packages"
  },
...
```

Note that you will need to adjust folder permissions if the `Path` is created outside of the BaGetter top-level directory. See [BaGetter folder permissions](#bagetter-folder-permissions).

## IIS server options

Settings such as the maximum package size can be configured for IIS in the appsettings.json file - see [IIS Server Options](../configuration.md#iis-server-options).
