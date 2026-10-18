| Download | Size | For |
|---|---|---|
| `Ebb-…-win-x64.zip` | about 70 MB | Windows 10/11, needs nothing else |
| `Ebb-…-win-x64-needs-dotnet8.zip` | about 3 MB | Windows with the [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) |
| `Ebb-…-linux-x64.tar.gz` | about 40 MB | 64-bit Linux (Intel/AMD), any distro |
| `Ebb-…-linux-arm64.tar.gz` | about 40 MB | ARM Linux (Raspberry Pi 4/5, Asahi, ARM laptops) |
| `ebb_…_amd64.deb`, `ebb_…_arm64.deb` | about 40 MB | Ubuntu, Debian, Mint, Pop!_OS |

**Windows:** unzip it and run **Ebb.exe**. Keep `Ebb.Helper.exe` next to it. Ebb isn't code-signed yet, so Windows may say **Windows protected your PC** the first time. Click **More info**, then **Run anyway**.

**Arch, Omarchy, Manjaro:** `yay -S ebb-bin` (or `paru -S ebb-bin`).

**Ubuntu, Debian:** `sudo apt install ./ebb_*_amd64.deb`

**Any other Linux:** unpack the tarball and run the installer, which needs no root and adds Ebb to your app menu:

```sh
tar -xzf Ebb-*-linux-x64.tar.gz
cd Ebb-*-linux-x64 && ./install.sh
```
