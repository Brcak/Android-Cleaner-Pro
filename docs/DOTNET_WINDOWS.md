# Android Cleaner Pro — portable Windows technician edition

## Download
Open [Windows .NET Build](https://github.com/Brcak/Android-Cleaner-Pro/actions/workflows/windows-dotnet.yml), choose a successful run, and download the `AndroidCleanerPro-Portable-win-x64` artifact. Extract the GitHub artifact and the included portable ZIP, then run `AndroidCleanerPro.exe`.

The application is written in C# / .NET 10 WPF. **Python, separate .NET installation, and separate ADB installation are not required.** Google's official Android SDK Platform Tools are downloaded at build time and embedded in the EXE. On first device operation, the app extracts ADB to the current user's local app-data cache and uses it automatically.

## Phone setup
Enable USB debugging on the customer's phone, connect using a USB data cable, unlock it, and approve the RSA prompt. Click Refresh Devices. Some Windows machines still need the manufacturer's USB driver; these system-level drivers cannot be embedded in a portable app.

## Build from source
Install the .NET 10 SDK on Windows and run `./build-dotnet-windows.ps1`. The build downloads official Google Platform Tools into the ignored `src/AndroidCleanerPro/Assets/platform-tools.zip` file, embeds them in the self-contained executable, and creates `dist/AndroidCleanerPro-Portable-win-x64.zip`. The previous `-BundleAdb` argument is accepted for compatibility but no longer necessary.

## Privacy and safety
ADB may create authorization keys and state in the Windows user profile. Its extracted tools are cached in LocalAppData. No installer is required, but portability does not mean zero host traces. Owner authorization is mandatory. Heuristic package review is not proof of malware. Uninstall for user 0 can erase app data. JSON reports contain device identifiers. The EXE is not code-signed.

**Build success and real-device operation are not yet verified.**
