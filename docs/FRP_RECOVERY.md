# FRP Recovery Assistant

Android Cleaner Pro includes a **Factory Reset Protection (FRP) Recovery Assistant** under **Tools → FRP Recovery Assistant**, the toolbar and the dashboard.

## What this feature does

- Explains Android Factory Reset Protection and why a device may request a previously synced Google Account or former screen lock after a reset.
- Guides the technician through verifying customer authorization and ownership documentation.
- Provides situation-specific instructions for locked-after-reset devices, forgotten passwords, forgotten account emails, phone resale/repair, and used devices.
- Provides manufacturer guidance for Samsung, Motorola, Google Pixel and other Android models.
- Opens official Google Account Recovery, Google Android device protection, and manufacturer support pages.
- Copies a plain-text, non-sensitive summary of the recommended steps.

The assistant works **without an ADB connection** because phones stuck at FRP setup generally cannot authorize USB debugging.

## What it does not do

**No FRP bypass, firmware exploit, account deletion, Google password extraction, bootloader unlocking, EDL commands, wiping or security circumvention is implemented.** A phone at the FRP lock screen cannot generally be activated using an arbitrary ADB command. The original owner must verify their account or use an authorized device-support process.

No Google credentials, recovery codes, IMEIs or phone serials are collected or saved by this window. The intake checklist is not saved as proof of ownership. A technician should follow their shop's separate document-verification policies.

## Official recovery methods

1. If the phone requests an account after reset, the owner should sign in **on the phone** using a Google Account previously added and synced to that phone. On eligible models, the phone may also offer a previous PIN/password/pattern.
2. If the account email or password is forgotten, the owner can use [Google Account Recovery](https://accounts.google.com/signin/recovery) on their own trusted device. They should never disclose passwords or verification codes to repair shop software.
3. Depending on the phone and security events, a password change may impose a waiting period (e.g., Samsung's guidance discusses a 24-hour delay).
4. If the original owner cannot verify their account, the customer should consult the original seller, carrier, manufacturer, or authorized service center with proof of purchase. The availability of an authorized resolution varies.

For a device that is **already unlocked**, the owner should remove Google accounts in Settings before transfer or factory reset. Resetting or reflashing a locked device does not reliably remove FRP and can make recovery more difficult.

## Sources

- [Google Android — Device Protection](https://support.google.com/android/answer/9459346)
- [Google — Account Recovery](https://support.google.com/accounts/answer/7682439)
- [Samsung — Google Device Protection](https://www.samsung.com/us/support/answer/ANS10003349/)
- [Motorola — Reset Phone / Factory Reset Protection](https://help.motorola.com/hc/1797/12/global/en-us/CGd0e1460.html)
- [Google Pixel — Device Protection](https://support.google.com/pixelphone/answer/6172890)

This feature is informational and should not be described as an FRP unlocking or removal utility.
