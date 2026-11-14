# Adware & Suspicious Apps investigation

Open **Detect Adware / Popups** in the Device Dashboard after authorizing the customer's Android device for USB debugging.

## 1. Scan installed applications

Press **Scan Third-Party Apps**. The tool reads the list of third-party packages and installer source, then collects read-only package metadata and, where relevant, Android's overlay AppOps state. It checks for:

- package names containing a few advertising-related or suspicious optimization keywords
- missing or unfamiliar package installers (weak indication only)
- declared or referenced overlay/draw-on-top permission
- AppOps explicitly reporting overlay permission as **allowed**
- accessibility service, notification listener, and APK install-related declarations

The report assigns a **review priority**, not a malware verdict. Many popular and legitimate apps use overlays, accessibility or notification listeners. A trusted installer does not guarantee an app is safe either. **No apps are deleted automatically.**

Scanning a large phone can take several minutes. Progress is shown and you can cancel. Some Android builds restrict inspection; those packages are marked **Incomplete**.

## 2. Identify a pop-up's currently focused app

Keep the unwanted ad visible on the phone and press **Who Is Showing This Ad? (Now)** to capture the active window's package name. You can also choose **Watch Foreground (15s)** and trigger the popup during the watch to record changes in focus.

The focused package can be the app displaying an ad, but it can also be the browser, launcher, a host app using an advertising SDK, or Android System UI. **Foreground activity alone does not identify the ad's actual source.** Use it only as a clue alongside the app review indicators.

## 3. Review or remove

Select a package to see its installer and the reasons it was flagged. If the customer explicitly authorizes the removal and understands that data may be deleted, choose **Uninstall Selected**. The existing protected system/vendor package checks still apply.

Use **Export Findings** to save a local JSON report. Reports contain device serials, installed package names and foreground observations, so treat them as sensitive customer information. Nothing is automatically uploaded.

## Limitations and privacy

This is a **heuristic adware investigation tool, not an antivirus engine**. It cannot inspect private app files, guarantee detection of malicious advertising, or attribute every popup to a particular app on modern Android. The scanner makes ordinary read-only ADB calls and does not need root. It works across Android brands where the relevant ADB diagnostics are available.

For more information on Android's supported diagnostics, see [Google's dumpsys documentation](https://developer.android.com/tools/dumpsys) and [AppOps implementation notes](https://android.googlesource.com/platform/frameworks/base/+/HEAD/core/java/android/app/AppOps.md).
