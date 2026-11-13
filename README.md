# Android Cleaner Pro — portable Windows technician suite

Native **C# / .NET 10 WPF** Android repair technician app. Dark dashboard, ADB device discovery, Android model/version, third-party app inventory, conservative heuristic review, single-package owner-authorized removal, and JSON reports.

## Portable version — no Python or installer
1. Open [Windows .NET Build](https://github.com/Brcak/Android-Cleaner-Pro/actions/workflows/windows-dotnet.yml).
2. Select a **successful** run and download the `AndroidCleanerPro-Portable-win-x64` artifact.
3. Extract the artifact and then the included `AndroidCleanerPro-Portable-win-x64.zip` to a folder or USB stick.
4. Run `AndroidCleanerPro.exe`.

The package bundles the self-contained .NET app and Google's official ADB Platform Tools. Windows USB drivers may still be required; enable USB debugging and approve authorization on the phone. See [portable installation and troubleshooting](docs/DOTNET_WINDOWS.md).

## Build locally
On Windows with the .NET 10 SDK, run `./build-dotnet-windows.ps1 -BundleAdb`. Output: `dist/AndroidCleanerPro-Portable-win-x64.zip`.

## Status and safety
**Experimental; Windows CI build and physical-device testing not yet verified.** This is not an antivirus product. Package-name heuristics do not establish malware. Uninstall for Android user 0 can delete app data; use only with customer authorization. Exported reports include device identifiers and must be protected. The ZIP is not signed. ADB may create authorization keys in the Windows user profile even when run from USB.

## Repository
- `src/AndroidCleanerPro/` — C# / WPF source
- `.github/workflows/windows-dotnet.yml` — portable ZIP build
- `build-dotnet-windows.ps1` — local publish and optional ADB bundle
- `docs/DOTNET_WINDOWS.md` — portable usage guide

The superseded Python prototype remains recoverable from Git history.
