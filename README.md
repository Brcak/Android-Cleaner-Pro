# Android Cleaner Pro — native Windows technician suite (C# / .NET 10)

**New native Windows edition:** C# + WPF, dark technician dashboard, ADB device discovery and connection status, device model/Android version, third-party app inventory, heuristic review flags, individually confirmed package removal, and JSON reports.

## Get the Windows EXE (no Python required)
1. Open [Windows .NET Build — GitHub Actions](https://github.com/Brcak/Android-Cleaner-Pro/actions/workflows/windows-dotnet.yml).
2. Open a **successful** workflow run (or run it manually).
3. Download the `AndroidCleanerPro-win-x64` artifact and extract it.
4. Run `AndroidCleanerPro.exe`.

**ADB is still required.** Download Google's Android SDK Platform Tools and put `adb` on PATH, or copy the full `platform-tools` directory beside the EXE. On the phone, enable USB debugging and approve the computer. Verify with `adb devices -l`. See the [native Windows installation guide](docs/DOTNET_WINDOWS.md).

## Build with .NET 10 SDK
On Windows, run `./build-dotnet-windows.ps1` in PowerShell. Output: `dist/win-x64/AndroidCleanerPro.exe`.

## Safety and status
This is an **experimental technician tool, not an antivirus product**. Review flags are keyword heuristics and are not confirmed malware. Uninstall is for Android user 0 and can delete customer data; use only with device-owner authorization. JSON reports contain customer device identifiers. No cloud uploads. **The native build has been committed but not yet verified on a Windows PC or physical phone.** The GitHub workflow must pass before a downloadable build is available.

## Previous Python prototype
The earlier PySide6 prototype remains under `android_cleaner/` for reference; see [legacy Python installation](docs/WINDOWS_INSTALLATION.md). Python is **not needed** for the native C# app.
