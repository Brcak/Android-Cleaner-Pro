# Android Cleaner Pro v0.1 — experimental MVP

Windows-oriented PySide6 GUI for ADB package inventory, conservative heuristic review, and technician-approved removal. **Not an antivirus product.**

## Requirements
- Windows 10/11, Python 3.10+
- Google Android SDK Platform Tools (`adb` on PATH)
- Android phone with developer options and USB debugging enabled, connected and authorized by its owner

## Run
```powershell
py -m venv .venv
.venv\Scripts\activate
pip install -r requirements.txt
python -m android_cleaner.gui
```

## Windows EXE build
Run `build_windows.bat` on Windows. The executable will be under `dist/`.

## Behavior and safety
- Enumerates **third-party packages only** (`pm list packages -3 -i`).
- Review labels are weak package-name/installer heuristics, **not confirmed malware**.
- Consumer mode inventories apps only; Technician mode allows single-package uninstall after confirmation.
- Uninstall uses `pm uninstall --user 0`; it is **not quarantine** and can delete app data.
- Some platform/vendor namespaces are blocked, but the protected list is incomplete.
- Requires device owner authorization. No root or bulk uninstall.
- Reports include device serial and package names; handle as sensitive customer data.
- No data is uploaded.

## Roadmap
Permission audits, device-specific allowlists, signed threat intelligence, consent-based reputation checks, reversible disable where supported, local case management, tests, and a signed installer.
