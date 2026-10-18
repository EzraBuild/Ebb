| Download | Size | Needs |
|---|---|---|
| `Ebb-…-win-x64.zip` | about 70 MB | Windows 10/11, nothing else |
| `Ebb-…-win-x64-needs-dotnet8.zip` | about 3 MB | the [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) |
| `Ebb-…-linux-x64.tar.gz` | about 40 MB | 64-bit Linux (Arch and Omarchy, Ubuntu, Fedora, …), nothing else |

**Windows:** unzip it and run **Ebb.exe**. Keep `Ebb.Helper.exe` next to it. Ebb isn't code-signed yet, so Windows may say **Windows protected your PC** the first time. Click **More info**, then **Run anyway**.

**Linux:** unpack it and run the installer, which needs no root and adds Ebb to your app menu:

```sh
tar -xzf Ebb-*-linux-x64.tar.gz
cd Ebb-*-linux-x64 && ./install.sh
```

Or run `./app/ebb` straight from the folder.
