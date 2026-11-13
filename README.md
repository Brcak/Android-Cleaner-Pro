# Android Cleaner Pro — portable Windows technician suite

Native **C# / .NET 10 WPF** Android repair technician application with dark dashboard, device discovery, Android model/version, third-party app inventory, heuristic review, individual owner-authorized removal, and JSON reports.

## One portable EXE — ADB built in
1. Open [Windows .NET Build](https://github.com/Brcak/Android-Cleaner-Pro/actions/workflows/windows-dotnet.yml).
2. Select a **successful** run and download `AndroidCleanerPro-Portable-win-x64`.
3. Extract the artifact and its portable ZIP, then run `AndroidCleanerPro.exe`.

The build embeds Google's official ADB Platform Tools inside the self-contained Windows EXE. No Python, separate .NET runtime, ADB download, or PATH configuration is needed. The app extracts ADB into a user cache on first use. USB drivers and USB debugging authorization may still be required.

## Build
On Windows with .NET 10 SDK, run `./build-dotnet-windows.ps1`. The build fetches official Google Platform Tools, embeds the ZIP, and produces `dist/AndroidCleanerPro-Portable-win-x64.zip`. See [portable setup guide](docs/DOTNET_WINDOWS.md).

## Safety and status
**Experimental; successful Windows CI build and physical phone tests have not yet been verified.** Not an antivirus product. Keyword flags are not malware verdicts. Uninstall can erase app data and requires device-owner permission. Reports contain customer device identifiers. Unsigned executable; ADB may create host authorization keys and cached extracted files.

## Repository
- `src/AndroidCleanerPro/` — native C# WPF source
- `.github/workflows/windows-dotnet.yml` — portable build workflow
- `build-dotnet-windows.ps1` — embed ADB and publish
- `docs/DOTNET_WINDOWS.md` — technician instructions

The earlier Python prototype remains recoverable from Git history.
