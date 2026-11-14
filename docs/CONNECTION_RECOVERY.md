# Connection Recovery for Malware-Blocked Phones

Open **Device → Connection Recovery**, **Tools → Connection Recovery**, or the dashboard/toolbar button. The tool works even when no phone is currently selected.

The assistant checks **ADB** and **Fastboot** separately and recommends next steps for:
- **device** — authorized ADB; normal inventory and adware scanning can work
- **unauthorized** — phone sees ADB but has not trusted the computer. Unlock and approve the RSA prompt on the phone
- **offline** — restart ADB server, check cable/USB drivers, reconnect
- **recovery** — normal Android app data and package manager may be inaccessible
- **sideload** — recovery permits installing verified OTA packages, not normal shell/inventory
- **Fastboot** — bootloader is reachable, but this is not an authorized Android ADB session
- **no devices** — check cable, port, Windows drivers, mode, power and owner access

**Restart ADB Server** is the only host-side service-changing action. It requires confirmation because it may interrupt other PC tools. No phone data is modified.

## When adware has taken over the screen

On a model supporting it, have the phone's owner restart in **Safe Mode** by hardware buttons or the power menu. Safe Mode temporarily disables most third-party apps; it can restore access to Settings and help isolate a problem app. Then the owner can enable USB debugging and approve the computer normally, or uninstall recently added apps from the phone's own UI.

The safe-mode key sequence varies by brand and model. Motorola's guide: https://help.motorola.com/hc/8020/11/global/en-us/CGjcb1208210742.html

## Security limitations

The software **cannot force ADB authorization, silently enable USB debugging, sidestep the Android RSA prompt, bypass FRP, or read private app data on an untrusted phone**. Android's official documentation describes the required on-device confirmation:
https://developer.android.com/tools/adb

Fastboot and recovery are different interfaces, and merely detecting one does not enable normal Android app management. Recovery sideload cannot run ordinary shell commands:
https://source.android.com/docs/core/ota/nonab/device_code

**Never unlock the bootloader, factory-reset, flash, or wipe a customer's phone just to obtain ADB access**, especially when the goal is adware removal and preserving customer data.

## Test plan

1. Run with no phone attached: both lists should be empty, no crash.
2. Connect an already-authorized Android phone: show `device`.
3. Revoke USB debugging authorization on a test phone: show `unauthorized`; program must not attempt access.
4. Exercise **Restart ADB Server**: show warning, restart host ADB, rescan.
5. On an intentionally offline or recovery-mode spare device, verify state-specific explanations.
6. Confirm **Device**, **Tools**, dashboard and toolbar launch the same window.

Compilation verifies .NET/WPF wiring, but physical-device testing remains necessary.
