# Android Cleaner Pro — Windows Installation and Technician Guide

> **Development release v0.2.** This is a technician-oriented ADB package inventory and review tool, **not a certified antivirus or comprehensive malware detector**. The Windows executable and updater are not prebuilt or published yet.

## 1. What you need

- Windows 10 or Windows 11 (64-bit recommended)
- A Windows account permitted to install applications
- Python 3.10 or later from https://www.python.org/downloads/windows/
- Android SDK Platform Tools (ADB) from https://developer.android.com/tools/releases/platform-tools
- A USB data cable (some cables only charge)
- An Android phone you own or are explicitly authorized to service, unlocked for USB debugging authorization
- Internet connection for the initial downloads

**Never install an untrusted APK, disable Play Protect, or root a customer's phone just to use this program.**

## 2. Install Python

1. Download a supported Windows Python version from python.org.
2. Run the installer; enable **Add Python to PATH** if offered.
3. Open **PowerShell** and verify:

```powershell
py --version
py -m pip --version
```

If `py` is not recognized, reinstall Python with the Python Launcher enabled or use `python` instead.

## 3. Install Android Platform Tools (ADB)

1. Download **SDK Platform-Tools for Windows** from the official Android Developers site.
2. Extract it to a stable location, for example `C:\Android\platform-tools`.
3. In PowerShell, test directly:

```powershell
& "C:\Android\platform-tools\adb.exe" version
```

4. Add `C:\Android\platform-tools` to your **user PATH** in Windows: Start → search **Edit environment variables for your account** → **Path** → **Edit** → **New** → paste the folder → OK.
5. Close and reopen PowerShell and verify:

```powershell
adb version
```

**Important:** The GUI expects `adb` to be available on PATH. Merely extracting Platform Tools is not enough.

## 4. Download and install Android Cleaner Pro

1. Open https://github.com/Brcak/Android-Cleaner-Pro
2. Select **Code → Download ZIP** (if the repository is private, sign in with an account that has access).
3. Extract to a folder such as `C:\Tools\Android-Cleaner-Pro`.
4. Open PowerShell in that extracted folder.
5. Create a virtual environment and install the GUI dependency:

```powershell
py -m venv .venv
.\.venv\Scripts\python.exe -m pip install --upgrade pip
.\.venv\Scripts\python.exe -m pip install -r requirements.txt
```

6. Launch:

```powershell
.\.venv\Scripts\python.exe -m android_cleaner.gui
```

You should see the dark **Technician Workstation** dashboard.

### If PowerShell says scripts are disabled

The commands above invoke `python.exe` directly and do **not** require activating the virtual environment or changing PowerShell execution policy.

## 5. Connect an Android phone

On the phone:

1. Open **Settings → About phone** (on Samsung, **About phone → Software information**).
2. Tap **Build number** seven times; enter the screen-lock PIN if requested.
3. Return to Settings and open **Developer options**.
4. Enable **USB debugging**.
5. Connect the phone with a USB **data** cable.
6. Unlock the screen and approve **Allow USB debugging?**. Check the PC's RSA fingerprint before approving.

On the computer, verify:

```powershell
adb devices -l
```

Expected output resembles:

```text
List of devices attached
R58XXXXXXX device product:... model:... device:...
```

If you see `unauthorized`, unlock the phone and accept its authorization prompt. If no device appears, try another cable/USB port; install the manufacturer's official USB driver if Windows does not recognize the device. Samsung's official Windows USB driver is available from Samsung Developer: https://developer.samsung.com/android-usb-driver

**Android versions and manufacturers vary.** Some enterprise-managed phones prohibit USB debugging.

## 6. Perform a technician inspection

1. Launch Android Cleaner Pro.
2. Click **Refresh devices** and select the authorized phone.
3. Click **Run inspection**.
4. Review the installed third-party packages, installer source, and review notes.
5. Treat **Review** as a heuristic prompt, not a positive malware diagnosis. Verify the app's identity, user consent, and business necessity before removal.
6. Select a package and click **Uninstall selected package** only after confirming the customer's authorization and a backup. Confirm the dialog.
7. Click **Run inspection** again to refresh the inventory.
8. Click **Export diagnostic report** to save a JSON report.

### Removal warning

The current tool invokes `pm uninstall --user 0`, which uninstalls a package for Android's primary user. This is **not a reversible quarantine** and may erase app data or break dependencies. Vendor package protections are incomplete. Do not remove unfamiliar system or work-profile packages. The current version does not scan file contents, inspect all Android profiles, or guarantee a malware-free device.

## 7. Build a Windows EXE

To build on Windows:

1. Install Python as above.
2. From the repository folder, run `build_windows.bat` (or execute these commands):

```powershell
py -m pip install -r requirements.txt pyinstaller
py -m PyInstaller --noconfirm --onefile --windowed --name AndroidCleanerPro launcher.py
```

3. Find the generated program at `dist\AndroidCleanerPro.exe`.

**ADB must still be installed and on PATH** on the technician PC. The EXE does not currently bundle ADB, provide a Windows installer, or implement automatic updates. Windows SmartScreen may warn about unsigned development executables; do not bypass warnings for files you cannot verify.

## 8. Common problems

| Symptom | What to check |
|---|---|
| `adb` not found | Install Platform Tools, add its directory to PATH, restart terminal/app |
| No device listed | USB data cable, port, USB debugging, Windows driver, unlocked phone |
| `unauthorized` | Approve the RSA fingerprint prompt on the phone |
| `offline` | Reconnect USB; run `adb kill-server` then `adb start-server` |
| Python module not found | Run commands in the extracted repository root; install requirements in the active environment |
| App opens but no scan | Confirm `adb devices -l` shows `device`; check errors shown in the GUI |
| Uninstall fails | Package may be protected, device-managed, or unavailable for user 0; do not bypass device policy |
| No installer/download on Releases | No official packaged release has been published yet |

## 9. Privacy and safe shop workflow

- Obtain explicit customer consent before accessing or changing the device.
- Record customer authorization and any requested exclusions outside the current application.
- Back up important customer data before uninstalling apps.
- Reports can contain device serial numbers and package lists. Store them securely and delete them under your shop's retention policy.
- Disconnect the phone and advise the customer to turn off USB debugging when service is complete.
- For suspected high-risk compromise, use official Android security guidance and consider a factory reset only after backup and customer approval.

## 10. Updating the software

**Automatic updates are not implemented yet.** For source installs, download a newer repository revision and reinstall Python requirements if they change. For EXE builds, rebuild on Windows. Future releases can add signed installers and authenticated/signed update verification. Never embed a GitHub personal access token in a distributed EXE.

## Official references

- Android Platform Tools: https://developer.android.com/tools/releases/platform-tools
- Android device setup: https://developer.android.com/studio/run/device
- Python for Windows: https://www.python.org/downloads/windows/
- Samsung USB driver: https://developer.samsung.com/android-usb-driver
