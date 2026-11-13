# Automatic update checks

On startup, Android Cleaner Pro checks the latest published GitHub Release for a newer version. If found, it asks the technician for permission, downloads the official `AndroidCleanerPro-Setup.exe`, verifies the GitHub Release SHA-256 digest, and launches an in-place Inno Setup update. It does **not** uninstall the old version first. Windows may ask for administrator permission.

The GitHub Actions workflow creates a numbered release for successful pushes to `main`, using tags `v0.3.<run_number>`. Updates are **opt-in**, not forced. Internet access is required to check/download, but not to use ADB.

This is checksum verification, **not code-signature verification**. For commercial distribution, add Authenticode signing and verify publisher identity before launching an update. GitHub Actions build artifacts are not updater feeds; only published Releases are used. A GitHub repository must be publicly readable for unauthenticated in-app update checks. If the repository is private, the app skips update checks; do not embed a personal GitHub token in desktop software.

An older installed build will not have the updater until the new installer is installed once.
