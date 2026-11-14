# Updating Android Cleaner Pro

The app checks the latest public GitHub Release when opened. A **Check for Updates** button also lets technicians check manually.

When a newer version is found, Android Cleaner Pro asks permission to download the official release installer. It verifies the installer with GitHub's published SHA-256 digest. Then it launches the **visible** Inno Setup wizard and closes the old app only after Windows starts that installer. The installer updates files in the existing installation directory; **uninstalling the previous version is not required**. Windows may ask for administrator permission.

### If updating fails or the app closes unexpectedly
1. Download the newest `AndroidCleanerPro-Setup.exe` directly from [GitHub Releases](https://github.com/Brcak/Android-Cleaner-Pro/releases).
2. Close Android Cleaner Pro normally.
3. Run the installer over the existing installation. Do **not** uninstall the old version first.
4. In the latest app, open **Check for Updates** if you want to retry an update.

Newer builds keep diagnostic logs at `%LOCALAPPDATA%\AndroidCleanerPro\logs\updater.log`. Copy that path into File Explorer to view it. The log does not contain Android phone package inventory.

The app does not silently run installers or forcibly relaunch itself during updates. A cancelled administrator prompt leaves the current application open.

### Release model and limitations
GitHub Actions publishes versioned releases for successful pushes to `main`. The updater uses published GitHub Releases, not Actions artifacts. Updates are optional and require internet access.

This verifies checksums from GitHub, but releases and installers are **not Authenticode code-signed**. Signing and publisher verification should be added before wide commercial distribution. The repository must remain public for unauthenticated update checks.

Existing installations made before this fix cannot gain the fixed updater until you **manually install the newer setup once**. A published build alone does not change files already installed on a customer's PC.
