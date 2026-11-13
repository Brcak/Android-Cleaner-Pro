# Android Cleaner Pro — native Windows C# edition

This is the new **C# / .NET 10 WPF** technician application. The old Python prototype remains in the repository for reference but is not needed to run the native version.

## Download a build
Open **Actions → Windows .NET Build** in GitHub. Select a successful run and download the `AndroidCleanerPro-win-x64` artifact. Extract it, then run `AndroidCleanerPro.exe`. The executable is self-contained: **Python and a separate .NET runtime are not required**. This is a CI build artifact, not a signed installer.

## Android connection requirements
The application still requires Google's **ADB** and the appropriate Windows USB drivers. Download Android SDK Platform Tools from https://developer.android.com/tools/releases/platform-tools and either add the directory containing `adb.exe` to your Windows PATH or place the complete `platform-tools` folder beside `AndroidCleanerPro.exe`. Enable Developer options and USB debugging on the customer's phone, unlock it, connect using a data-capable USB cable, and approve the RSA authorization prompt.

Test in PowerShell: `adb devices -l`. If it says `unauthorized`, approve on the phone; if it shows no devices, check cable/driver/USB debugging. If it says `device`, click **Refresh Devices** in the application.

## Build from source
Install the .NET 10 SDK on Windows. Run `./build-dotnet-windows.ps1` in PowerShell. The app is published under `dist/win-x64/`.

## Features and limits
Device discovery with explicit unauthorized/offline status; model and Android version; third-party app inventory; conservative review flags; one-at-a-time owner-authorized uninstall; JSON reports. **Not an antivirus scanner.** Uninstall for Android user 0 can erase app data. There is no firmware flashing, bypass of device security, full backup, or automatic updating yet. Do not use on customer phones without authorization. The report contains device identifiers and package inventory; protect it.

## Next steps
Test against physical devices, add ADB setup assistant and Windows USB troubleshooting, add app and file management, diagnostic modules, signed releases and installer, and only then a verified updater.
