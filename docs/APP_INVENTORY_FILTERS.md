# App Inventory: Categories and Installation-Date Filters

Android Cleaner Pro's **Scan Installed Apps** now reads both ordinary user apps and Android system packages, then shows an interactive inventory with **Category**, **Type**, **First installed**, **Last updated**, **Installer**, **Review**, and package name.

## Filters

- **Category:** All categories, Android / System, Google, Banking / Finance, Samsung, Motorola, T-Mobile / Metro, Other familiar vendors, Other apps.
- **App type:** All apps, User apps, System apps.
- **First installed:** Any date, within the last **1 day**, **2 days**, **7 days**, or **30 days**, plus **Date unavailable**.
- **Search:** Match the package name, category or reported installer.
- **Hide familiar vendor/system categories:** Focus on less recognizable third-party apps. Choosing an explicit vendor category overrides this hiding option.

Filters combine together and update the table without rescanning the phone. The result count displays **shown / scanned**. **Export Visible JSON** exports only the currently visible rows and includes the filter settings; it does not silently export hidden rows.

## Where the data comes from

The inventory uses regular ADB commands:

- `pm list packages -3 -i` for user apps and installer information.
- `pm list packages -s -i` for system apps.
- A single `dumpsys package` for **firstInstallTime** and **lastUpdateTime** when available.
- `date +%z` for the connected phone's local UTC offset, so dates can be displayed in Windows local time.

The **First installed** column is the timestamp *Android reports for that package*, not necessarily the original date the owner downloaded it. Preloaded apps, restored apps, transferred apps, OS upgrades and reinstallations can affect these timestamps. **Last updated** is independent of first install.

Some Android versions, manufacturers, USB connections or restricted shells do not expose these dates. Such apps say **Not reported**. They will not falsely appear in a recently installed filter; choose **Date unavailable** to find them.

## Important safety caveat

Vendor and banking categories are grouped using their **package namespace** as a convenience, **not verified signing certificates, official publisher records, malware scanning or guarantees of safety**. An unfamiliar app can be legitimate; an app claiming a known vendor or banking namespace can be suspicious. **Do not assume an app is safe because it appears under Google, Banking, Samsung or System.** A system package is classified by Android's package-manager system flag, not by its filename.

The inventory **blocks uninstalling system apps** through this window. Non-system app uninstall still requires owner authorization and a one-app-at-a-time confirmation. Deleting any app can erase user data.

The dedicated **Detect Adware / Popups** investigation still scans third-party applications and does not use vendor categories as evidence that an app is safe.

## Troubleshooting

If every install date says `Not reported`, check that your phone authorized USB debugging, that the phone can answer `adb shell dumpsys package`, and that `adb shell date +%z` returns a valid time-zone offset. The app will still show package categories and installer information when date metadata cannot be read.
