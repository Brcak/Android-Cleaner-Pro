# Simple Mode and Advanced Tools

Android Cleaner Pro now opens to a **guided Home page** with a minimal technician workflow.

## Typical phone cleanup

1. **Find Phone** — connect an Android phone with a USB data cable and approve USB debugging on the phone. Select the phone in the device selector. If the phone cannot be authorized, use **Connection Help**.
2. **Scan for Problems** — launch the Security Scanner. Review the evidence for unwanted advertising or excessive permissions; a score does not certify malware.
3. **View Installed Apps** — inspect recently installed packages, filter by manufacturer or install date, and choose an app to review or uninstall only with the owner's permission.
4. **Create Repair Report** — use Repair Jobs to record what happened and create a printable customer report.

The sidebar contains **Home**, **Installed Apps**, **Security Scan**, **Repair Reports**, and **Advanced Tools**. The top toolbar has only the most-used actions.

## Advanced Tools

The **Advanced Tools** page gathers operations that are unnecessary for most basic cleanups:
- Android Log Viewer (diagnostic logs)
- Detailed phone information
- Hazard Database and review history
- Firmware / Fastboot tools
- FRP owner account recovery
- Reboot to recovery
- Safe Mode guidance
- Restart ADB server
- Stop ADB services

For keyboard navigation or experienced users, **Advanced** is also a menu at the top of the window. No specialist capability was removed.

## Design constraints and safeguards

- Normal Home never offers a destructive action.
- Device scanning and ADB shell operations still require authorized debugging access; the UI does not bypass security.
- Uninstall and reboot remain explicit, individually confirmed operations.
- Firmware flashing still has its own separate warnings and safety checks.
- Installed-app and hazard status labels are for triage, not malware certification.
- Manufacturer name categories are not publisher/signature verification.
- ADB server stopping may affect unrelated software and is intentionally hidden from Home.
- The active navigation item is highlighted, and app inventory remains loaded when the same device is refreshed.

## Visual acceptance checklist

- Launch with no phone: Home shows guided actions and Connection Help.
- Sidebar buttons each open their correct pages and highlight appropriately.
- Advanced tools remain available on the Advanced page.
- Connect a phone; refresh device list; confirm cached app results are not cleared when selection is unchanged.
- Scan on an authorized device and filter the inventory.
- Confirm destructive actions still ask permission.
- Resize to minimum window size and confirm Home/Advanced content scroll.
- Windows compilation verifies that XAML event handlers exist; actual desktop click testing remains necessary.
