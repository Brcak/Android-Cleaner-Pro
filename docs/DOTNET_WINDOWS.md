# Android Cleaner Pro — portable Windows technician edition

The application is written in C# / .NET 10 WPF, not Python. The old Python prototype was removed from the current branch.

## Portable download — no installer
1. Go to [Windows .NET Build](https://github.com/Brcak/Android-Cleaner-Pro/actions/workflows/windows-dotnet.yml).
2. Open a **successful** run and download the `AndroidCleanerPro-Portable-win-x64` artifact.
3. Extract the GitHub artifact, then extract `AndroidCleanerPro-Portable-win-x64.zip` to a folder or USB drive.
4. Run `AndroidCleanerPro.exe` from the extracted folder.

The portable package contains a self-contained .NET Windows executable and Google's official Windows Android SDK Platform Tools in `platform-tools/`. No Python, .NET runtime installation, or ADB PATH configuration is needed. **Do not run the EXE directly from inside the ZIP.** Windows USB drivers may still need installation on the host computer, and Windows may show an unsigned-app warning.

## Connect a phone
On the owner's Android device, enable Developer options and USB debugging, connect with a data-capable USB cable, unlock the phone and approve the USB debugging RSA prompt. In the application click **Refresh Devices**. For troubleshooting, run `platform-tools/adb.exe devices -l` from the extracted portable folder. `unauthorized` means approve on the phone; no devices usually means cable, USB mode, driver, or debugging configuration.

## Build from source
Install the .NET 10 SDK on Windows. Run `./build-dotnet-windows.ps1 -BundleAdb` to download the official Google Platform Tools and build `dist/AndroidCleanerPro-Portable-win-x64.zip`. Without `-BundleAdb`, the ZIP does not contain ADB.

## Privacy and limitations
The application does not require an installer and does not intentionally store configuration or reports outside the chosen export path. ADB itself may create host authorization keys and state in the Windows user profile; portability does **not** imply zero host traces. ADB USB drivers are system-level and cannot be made portable. Device owner authorization is mandatory. Heuristic review flags are not proof of malware. Package removal can erase app data.

The new portable build workflow has been configured, but a successful Windows CI build and a real-phone test have **not** been verified yet. This is not a signed release.
