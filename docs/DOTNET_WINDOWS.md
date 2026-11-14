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


## Expanded Phone Information window
Select an authorized phone and click **Phone Info**. The redesigned detail window reports device manufacturer, brand, model number, product name, device code name, serial identifiers when available, Android version/API/build/security patch, bootloader, battery level/status/temperature/voltage, CPU ABI, RAM, storage usage and screen dimensions/density. Fields not accessible through standard ADB are clearly labeled as not reported.

IMEI 1 and IMEI 2 are **manual-entry fields**. Modern Android restricts IMEI retrieval for ordinary ADB shell clients. Obtain values with the device owner's permission through the phone itself (typically dial `*#06#` or use Settings → About phone). The application does not silently bypass Android's access controls, and manually entered IMEIs are not saved or included in copied summaries. The Copy device summary action excludes IMEI values.


## ADB service controls and phone reconnect

The **Connected Devices** area includes two maintenance buttons:

- **Stop All ADB Services**: After confirmation, sends `adb kill-server` and terminates remaining accessible Windows `adb.exe` processes. It clears stale device listings. This can interrupt Android Studio, other Android repair tools, and active ADB sessions on the PC. It does **not** remove or change any data on the phone. Processes started by another Windows account or elevated programs may not be stoppable without sufficient permissions.
- **Restart ADB + Detect Phone**: Stops old ADB processes, starts a fresh ADB server, then refreshes connected devices. The USB authorization prompt may reappear; unlock the phone and approve it.

Normal **Refresh Devices** does not kill ADB. During a verified in-app installer update, Android Cleaner Pro now stops ADB before it launches the installer to avoid locking bundled Platform Tools executables.

If the phone still does not show after ADB restart: confirm USB debugging is enabled, use a data-capable cable, approve the authorization dialog, try a different USB port, and check Windows USB drivers. ADB can't bypass phone authorization. If another Android tool immediately restarts its own ADB server, close that tool before retrying.
