# Android Cleaner Pro — Security Scanner 2.0

Security Scanner 2.0 is a **read-only diagnostic and triage tool**, not an antivirus engine or a claim of malware certification.

## What changed

- **App installation timelines**: the scanner now uses the same Android-reported `firstInstallTime` inventory as App Inventory, where available, to show install time and filter apps from the last **24 hours, 48 hours, 7 days, 30 days**, or those with unknown dates. A restored or reinstalled app may have a misleading first-install time; do not infer the date a customer's problems began from the timestamp alone.
- **Enabled accessibility services**: the scanner requests the Android secure setting `enabled_accessibility_services` and matches complete package IDs. An **enabled** service is a stronger operational clue than merely declaring a service in the APK manifest. It is **not proof** the service is malicious.
- **Enabled notification listeners**: reads `enabled_notification_listeners` when Android exposes the setting, and distinguishes **enabled** listeners from mere declarations.
- **Overlay AppOps**: checks `SYSTEM_ALERT_WINDOW` via `cmd appops get` when the package references overlay permissions; explicitly allowed overlays get stronger review priority than package name keywords.
- **Permission declarations and weak context**: APK installation permission, accessibility/notification declarations, unknown install source and advertising-related names are review clues, not security verdicts.
- **Recent install filter**: a package whose Android first-install time falls in the last 48 hours gets a weak review signal; it is never classified hazardous solely for that date.
- **Evidence visibility**: the scanner displays evidence strength, risk score, review priority, and reasons for review. All user-visible dates can be filtered or searched without rescanning.

## Important limits

- Google, Samsung, Motorola, finance and other package-name categories are **not cryptographic identity verification**. No package is silently whitelisted as safe. A package with a familiar name still gets security review.
- **APK-signature comparison against an authenticated vendor allowlist is not implemented.** It must not be inferred from package name or install source.
- Android may deny `settings get secure`, `dumpsys package`, or `appops` on some models; missing information is not proof a service is disabled.
- Permission requests do not establish abuse; a screen reader, password manager, automation tool or legitimate notification app may have many of these capabilities.
- A foreground window capture identifies the owner of the focused window, **not necessarily** the producer of an advertising overlay.
- Scanner results are **not automatically entered as confirmed hazards** in the Hazard Database. The technician must review the evidence, choose a marking, and document a reason.

## Safe workflow

1. Get the owner's authorization and connect an ADB-authorized Android phone.
2. Run **Detect Adware / Popups → Scan Third-Party Apps**.
3. Filter by first-install timeframe or elevated review priority.
4. Select a package and review its Android-reported evidence.
5. If a popup is present, capture the foreground window for another clue.
6. Open **Mark / View Hazard DB** to document the technician's assessment.
7. Only after owner confirmation, uninstall one specific app; the uninstall result is audited separately.

This feature never automatically deletes apps or bypasses Android debugging authorization.

## Read-only Android sources

- [ADB shell and package management](https://developer.android.com/tools/adb)
- [Android PackageManager shell implementation](https://android.googlesource.com/platform/frameworks/base/+/main/services/core/java/com/android/server/pm/PackageManagerShellCommand.java)

## Testing

Windows compilation is automated through GitHub Actions. Actual OS settings, package timestamps and AppOps availability vary by Android version, vendor and active user profile, and require testing on a physical phone.
