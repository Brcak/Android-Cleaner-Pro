# Android Cleaner Pro — standard Windows desktop edition

This project uses C# and .NET 10 WPF. The experimental portable/embedded-ADB packaging was discontinued to simplify builds.

## Download
Open [Windows .NET Build](https://github.com/Brcak/Android-Cleaner-Pro/actions/workflows/windows-dotnet.yml), choose a **successful** run, and download the `AndroidCleanerPro-Windows` artifact. Extract it and launch `AndroidCleanerPro.exe`.

Install the [.NET 10 Desktop Runtime for Windows](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) if prompted. This is a normal multi-file Windows application build, **not yet an MSI/Setup.exe installer**.

## Connect an Android phone
Install [Google Android SDK Platform Tools](https://developer.android.com/tools/releases/platform-tools). Add its directory to Windows PATH, or place the complete `platform-tools` directory alongside the application. Enable USB debugging on the device, connect using a data-capable USB cable, unlock it, and approve the computer's RSA authorization prompt. Some devices need an OEM USB driver.

Test in PowerShell with `adb devices -l`. If the device shows `unauthorized`, approve on the phone. If no device appears, check the cable, USB drivers and debugging.

## Build from source
Install .NET 10 SDK on Windows. Run `./build-dotnet-windows.ps1`. Output: `dist/Windows/`.

## Safety
The app is experimental and is not antivirus software. Package review labels are heuristics, not malware verdicts. Uninstall can erase app data; use only with owner authorization. JSON reports contain device identifiers. Windows build success and real-device operation remain unverified.
