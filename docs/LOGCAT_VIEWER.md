# Android Log Viewer

Open **Tools → Android Log Viewer** or **Security Review → Open Android Log Viewer** with an ADB-authorized device selected.

The window runs a bounded **read-only** snapshot command, `adb shell logcat -d -t 500 -v threadtime`, when you click **Read Latest 500 Lines**. It does not clear the device's logs or start an indefinite capture.

Filters allow all lines, warnings/errors, or errors only, plus free-text search. **Save Visible Logs** exports just the lines currently displayed, after explicit confirmation that customer logs can contain private information.

On newer Android builds, ordinary ADB may have restricted log visibility. Logs can help explain app crashes or advertising-related activity, but a log match alone does not prove an app is malware.

If no phone is authorized for ADB, use **Connection Recovery** instead. The viewer does not bypass device security.
