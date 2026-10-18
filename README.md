# Ebb

**Why is my C: drive full?** Ebb scans a drive, explains what's using the space in plain words, and helps you clean it up safely.

Most disk tools show *where* the space went. Ebb also tells you *what it is* and *whether it's safe to remove*, especially for the developer files other tools don't recognise: WSL and Docker disks, `node_modules`, package caches and old SDKs.

## Download

Get it from **[ebb-tau.vercel.app](https://ebb-tau.vercel.app)** or [Releases](https://github.com/EzraBuild/Ebb/releases), unzip it and run `Ebb.exe`. Windows 10/11, 64-bit.

- `Ebb-…-win-x64.zip` (about 70 MB) needs nothing installed.
- `Ebb-…-win-x64-needs-dotnet8.zip` (about 3 MB) needs the [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0).

Ebb isn't code-signed yet, so Windows may say **Windows protected your PC** the first time. Click **More info**, then **Run anyway**.

## What it finds

| Developer | Windows | Personal |
|---|---|---|
| WSL disks (`ext4.vhdx`) | Temporary files | Old installers and zips in Downloads |
| Docker disk and leftovers | Windows Update downloads | Large videos |
| `node_modules` in your projects | Windows upgrade leftovers, `Windows.old` | Exact duplicate files |
| npm, NuGet, pip, Yarn, pnpm caches | Old system file versions (WinSxS) | |
| Older .NET SDKs | Hibernation file, page file | |
| Visual Studio caches | Recycle Bin | |

Each finding is marked **SAFE** (comes back by itself), **CHECK FIRST** (probably fine, but look) or **KEEP**, with the reason.

A **Biggest folders** view lets you browse anything the findings don't explain.

## How cleaning works

1. Tick what you want to clean.
2. **Review**: Ebb shows exactly what will happen. You see every file and folder with its size and a tick box, plus the exact command for anything that runs one. Long lists have a filter: type a folder or project (e.g. `Desktop/count`), then **Untick these**. Nothing changes until you press **Clean up**.
3. Ebb does only what you approved, then shows what it freed.

Safety rules built in:

- **Recycle Bin by default.** Untick it to delete permanently. Windows asks before deleting anything too big for the bin.
- **Except `node_modules`**, which is deleted for good (and shown as such before you confirm): `npm install` brings it back, and sending hundreds of thousands of files to the bin is slow and frees nothing until it's emptied. Ebb deletes it many files at a time, about 10× faster than one by one. A folder that's in use (say, by a running dev server) is left untouched. Links inside it (pnpm) are removed, never followed.
- **Each tool's own command where one exists:** `docker system prune -f`, `dotnet nuget locals all --clear`, `npm cache clean --force`, `pip cache purge`, `yarn cache clean`, `pnpm store prune`.
- **Never touches system files directly.** Windows, Program Files and ProgramData are off limits. System leftovers are handled by Windows' own tools: Disk Cleanup, `DISM /StartComponentCleanup` and `powercfg /hibernate off`.
- **A path guard checks every item twice:** once when the plan is made, and again right before it's removed. It refuses drive-level items, your main folders themselves, other users' files, system files and links.
- **Everything is logged** to `%LOCALAPPDATA%\Ebb\cleanup-log.jsonl`.

## Shrinking WSL and Docker disks

WSL and Docker keep everything in a `.vhdx` file that grows but never shrinks. Ebb gives the empty space back without deleting anything:

1. Trims free space inside the distro (`fstrim`).
2. Runs `wsl --shutdown`. For Docker, quit Docker Desktop first.
3. Compacts the file with admin rights: `Optimize-VHD` if Hyper-V is installed, otherwise `diskpart` with the disk attached **read-only**.

Admin steps run in `Ebb.Helper.exe`, a small separate program that only accepts three fixed operations: shrink an existing `.vhdx`, turn hibernation off, and run DISM component cleanup. Windows shows a UAC prompt each time.

## Build and run

Needs Windows 10/11 and the .NET 8 SDK.

```powershell
dotnet build
dotnet run --project src/Ebb.App
dotnet test
```

To get a release build (about 8 MB, needs the .NET 8 Desktop Runtime):

```powershell
dotnet publish src/Ebb.App -c Release -o publish
.\publish\Ebb.exe
```

Or one that needs nothing installed (about 170 MB):

```powershell
dotnet publish src/Ebb.App -c Release -r win-x64 --self-contained -o publish
```

`Ebb.Helper.exe` must stay next to `Ebb.exe`.

To release a version, push a tag like `v0.1.0`. GitHub Actions runs the tests, builds both zips and attaches them to a new release.

The tests only use fake folders in `%TEMP%\EbbTests` and fake tools, and never touch your real files. One opt-in test moves a tiny temp file to your real Recycle Bin. Run it with `$env:EBB_RECYCLE_TEST = "1"; dotnet test`.

## Project layout

```
src/Ebb.Core     scanner, detectors, cleanup planner/executor, path guard, log, VHDX shrinking
src/Ebb.Helper   the admin helper (fixed operations only)
src/Ebb.App      WPF + WPF-UI app: monochrome, square corners, Geist
tests/Ebb.Tests  xUnit tests (sandboxed)
site             the website (Next.js on Vercel, deploys on every push)
```

## License

MIT, see [LICENSE](LICENSE).

## Credits

[Geist and Geist Mono](https://github.com/vercel/geist-font) fonts are by Vercel, licensed under the SIL Open Font License (see `src/Ebb.App/Fonts/OFL.txt`). Built with [WPF-UI](https://github.com/lepoco/wpfui) and the [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet).
