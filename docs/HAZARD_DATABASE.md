# Hazard App Database & Removal History

**Android Cleaner Pro → Tools → Hazard App Database** opens a local SQLite database of manually reviewed apps and app-removal outcomes. The Adware Scanner also has a **Mark / View Hazard DB** button, prefilled with the selected app's evidence. The App Inventory has a Hazard Database button that preselects the currently selected package.

## What is stored

The SQLite file is created at `%LOCALAPPDATA%\AndroidCleanerPro\hazards.sqlite3`, outside the installation folder, so installer updates do not overwrite it. Two SQL tables are created automatically:

- `hazard_apps`: package name, technician classification (**Review**, **Confirmed by technician**, **Cleared**), a written reason, and the timestamp the classification was last changed.
- `removal_events`: timestamp (UTC), package name, short salted device code, UI source (**App Inventory** or **Adware Scanner**), outcome (**Success** or **Failed**) and ADB result/error. Every actual uninstall attempt made inside either UI is logged. Confirmation cancellations are not uninstalls and are not logged.

**An uninstall is not evidence an app was malware.** The software never automatically confirms that an app is hazardous, even when an adware scan assigned a high review priority. Technicians must select the package, write a reason and intentionally save a classification. Clearing a mistaken suspicion changes the marking to **Cleared** rather than hiding its history.

The local device code uses a random, per-installation HMAC salt saved alongside the database in `hazards-device-salt.bin`; no IMEI, Google account, full ADB serial or customer passwords are saved. Reports and the database still contain package names and other business-sensitive diagnostic information. Treat them as confidential customer records.

## Using it

1. Connect an authorized phone and run the regular App Inventory or Adware Scanner.
2. Select a suspected package and open **Hazard Database / Mark / View Hazard DB**.
3. Enter evidence and choose `Review`, `Confirmed by technician`, or `Cleared`.
4. Save the marking. This does **not** remove or change the app on the phone.
5. If the owner authorizes removal, use **Uninstall Selected** in the inventory or adware scanner and confirm. The resulting ADB operation is recorded under **Uninstall History**, including failures.
6. Review records without a phone connected, refresh the tables, or export a JSON copy using **Export JSON History**.

The default history table shows the most recent 500 events; export includes up to 10,000. The database is stored only on this Windows computer and is not synced or uploaded automatically. There is no built-in bulk delete, cloud blacklist, universal reputation service or automated removal.

## Caveats

- A manually marked status is a technician finding, not a certified malware determination.
- Package names are not verified publisher signatures and can be reused by different applications.
- This audit only knows removals initiated **inside Android Cleaner Pro**, not app deletions made on the phone, through other utilities or before this feature existed.
- The application cannot override Android's ADB authorization requirements, and cannot uninstall system apps through the standard inventory.
- If the SQLite database cannot be written, the app reports a logging failure rather than claiming the removal was recorded.
- Backing up `hazards.sqlite3` and its salt file while the application is closed is recommended before transferring the app to another PC.
