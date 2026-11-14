# Android Cleaner Pro — Windows Technician Edition

Native C# / .NET 10 WPF application for owner-authorized Android diagnostics and app inventory.

## Install on Windows
Download `AndroidCleanerPro-Setup.exe` from the [latest published Release](https://github.com/Brcak/Android-Cleaner-Pro/releases/latest). Run the normal Windows installer. It includes Google's official Android Platform Tools (ADB); no separate ADB PATH setup is necessary. The PC still may need the phone manufacturer's USB driver, and the phone must approve USB debugging.

The [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) is required. The installer does not yet install it automatically.

## Software updates
The app checks for newer published GitHub Releases on startup. You can also use **Check for Updates**. After you approve an update, the app downloads the official installer, verifies its published SHA-256 checksum, launches the visible installer, and closes the old app. The new installer replaces the existing version without uninstalling first.

**Important for older installs:** A prior updater could close the app while starting its installer silently. If that happens, manually install the latest version from GitHub Releases **once** to get the newer, safer updater. For diagnostics, see [Update Troubleshooting](docs/UPDATES.md); logs are written to `%LOCALAPPDATA%\AndroidCleanerPro\logs\updater.log`.

## Desktop menu, toolbar and settings
The main window now includes a conventional **File / Device / Tools / Help** menu and a quick-access toolbar for refreshing phones, phone information, adware diagnostics, Settings, update checks and About. **Tools → Settings** lets technicians choose whether to check for updates at launch and optionally use a custom ADB executable. Preferences are saved per Windows user in `%LOCALAPPDATA%\\AndroidCleanerPro\\settings.json`; the default continues to use bundled ADB. **Help → About** displays the installed version, .NET runtime, and project links. See [Settings and navigation](docs/SETTINGS_AND_MENUS.md).

## Features
- Device discovery and connection status
- Phone manufacturer/model, Android version, battery and storage data (when available over ADB)
- Third-party app inventory and conservative package-name review
- **Adware & Suspicious Apps** investigation: overlay permission/AppOps signals, unusual installation sources, adware-related keywords, foreground popup-window capture and short observation, cancellation, and report export ([guide](docs/ADWARE_REVIEW.md))
- Single-package uninstall with explicit technician confirmation
- Recovery reboot with confirmation; manual Safe Mode guidance
- **Firmware / Fastboot Tools**: detect Fastboot devices, inspect bootloader state and verify firmware image checksums; limited, manually confirmed single-partition flashing only for already-unlocked compatible devices ([guide](docs/FIRMWARE_FLASHING.md)). Samsung Odin/Download Mode flashing is not implemented.
- **FRP Recovery Assistant**: owner-verification checklist and official Google/Samsung/Motorola/Pixel account-recovery instructions; works without ADB, never bypasses FRP ([guide](docs/FRP_RECOVERY.md)).
- JSON reports

App icons are not yet implemented. Adware indicators and foreground observations are not proof of malware or proof of which app generated an advertisement. The tool is experimental, not an antivirus product. Package-name flags do not establish malware, and uninstalling can erase app data. Use only with the device owner's permission. Reports contain device identifiers.

## Developers
Install the .NET 10 SDK on Windows and run `./build-dotnet-windows.ps1`. CI builds and Inno Setup releases use [Windows .NET Build](https://github.com/Brcak/Android-Cleaner-Pro/actions/workflows/windows-dotnet.yml).

The older Python prototype is available through Git history.
