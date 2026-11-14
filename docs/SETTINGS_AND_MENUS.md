# Navigation, Toolbar & Settings

Android Cleaner Pro includes a conventional Windows menu bar with **File**, **Device**, **Tools**, and **Help** menus. A quick-access toolbar appears below it.

## Menus
- **File**: Export App Report, Settings, Exit.
- **Device**: Refresh Devices, Restart ADB and Detect Phone, Stop All ADB Services, Phone Information, Reboot into Recovery.
- **Tools**: Scan Installed Apps, Detect Adware / Popups, Safe Mode Help, Settings.
- **Help**: Documentation, Check for Updates, About Android Cleaner Pro.

The toolbar offers shortcuts for Refresh, Phone Info, Adware Scan, Settings, Check Updates and About. It does not replace the existing device and app controls.

## Settings
Open **Tools → Settings** (also available under **File** or from the toolbar).

**Check for updates on startup** is on by default. Turn it off to skip automatic checks when you start the app. You can still use **Help → Check for Updates** at any time. Updates require approval and launch the normal Windows installer.

**Custom ADB executable (optional)** is blank by default. This uses the bundled `platform-tools/adb.exe` when present, otherwise the system PATH. You can browse to an existing `adb.exe`, test its version, or restore the bundled default. An invalid path cannot be saved. Changes take effect on the next ADB command. Windows USB drivers and phone authorization are still required.

Settings are local to your Windows user at `%LOCALAPPDATA%\AndroidCleanerPro\settings.json`. The settings file stores only the update-check preference and optional ADB path. It does not contain connected phone data, IMEIs, customer package inventory, or serial numbers.

## About
**Help → About Android Cleaner Pro** shows the installed application version, the active .NET runtime and links to the source repository, release downloads and Google's official ADB documentation.

The Windows installer updates the existing application; no uninstall is needed first. The release remains an experimental diagnostic utility and does not certify an app as malware.
