# Android Cleaner Pro — Firmware / Flashing Tools

## Current scope: limited Fastboot flashing

Android Cleaner Pro now includes a **Firmware / Fastboot Tools** console, accessible from the dashboard, toolbar or **Device → Firmware / Fastboot Tools**.

This is a cautious first release for technicians working on owner-authorized Android devices. It is **not** a general-purpose firmware repair suite or an Odin replacement.

### Supported actions

- Reboot a currently authorized ADB phone into bootloader mode (with confirmation)
- Detect devices visible through `fastboot devices -l`, even if no normal Android session is running
- Read the bootloader's `product`, `unlocked`, `current-slot`, `is-userspace` and `secure` variables where supported
- Select one `.img` file, compute its SHA-256, and optionally compare it to a checksum supplied from a trusted firmware source
- **Flash exactly one explicitly approved partition image** using Fastboot on a device whose bootloader is confirmed unlocked, with product/codename and exact-phrase confirmations
- Reboot a Fastboot device into Android (with confirmation)

Only `boot`, `init_boot`, `vendor_boot`, and `recovery` are supported by the limited image flasher. **Not every device supports these partitions**, and on some devices the partitions are slotted or behave differently. The operator must follow the OEM's device-specific instructions.

### Hard safety blocks

The tool deliberately does **not** issue `fastboot flashing unlock`, `fastboot flashing lock`, `fastboot erase`, `fastboot -w`, FRP bypass, critical bootloader / radio / modem writes, partition-table operations, automated `flashall` / `update` commands or OEM flashing scripts.

Before writing a single partition, it requires:

1. A selected Fastboot device with a confirmed unlocked bootloader.
2. Device product/codename reported by Fastboot, matching the manually entered firmware product.
3. A nonempty, existing local `.img` file; SHA-256 computed locally and verified again immediately before the write.
4. Customer authorization, a data backup and compatibility confirmation.
5. Typing `FLASH <device-product>` and accepting a final dialog showing device, partition, file and SHA-256.

**SHA-256 alone establishes file integrity, not firmware compatibility or authenticity.** Check the firmware's source and exact device version before proceeding. If available, paste the vendor-provided SHA-256 into the window to reject a mismatched file.

The app cannot prevent every brick, data loss or flashed mismatched firmware. A USB interruption or an incorrectly chosen image can leave a device unbootable. If Flash reports a failure, do not blindly retry.

## Manufacturer support

- **Pixel, Motorola and some other brands:** The bootloader may support Fastboot diagnostics and unlocked-device image flashing, depending on model, carrier and OEM restrictions.
- **Samsung Galaxy:** Samsung commonly uses its own **Download Mode** rather than standard Fastboot. Samsung/Odin flashing is **not implemented**, and Samsung firmware packages should never be sent through this Fastboot interface.

The app does not automatically download firmware or guarantee manufacturer images are compatible.

## Prerequisites

- Windows with Android Cleaner Pro installed (includes official Google Platform Tools, including `fastboot.exe`).
- Suitable Windows USB / Fastboot driver for the specific device.
- Owner consent and backup of important data.
- Exact manufacturer firmware for the exact phone model / codename.
- A bootloader that is already legitimately unlocked. **Unlocking a bootloader generally causes a factory reset**; this tool does not unlock it.

### Official documentation

- [Android Open Source Project — Flash with Fastboot](https://source.android.com/docs/setup/test/running)
- [Android Open Source Project — Bootloader locking / unlocking](https://source.android.com/docs/core/architecture/bootloader/locking_unlocking)
- [Samsung — Download mode information](https://www.samsung.com/us/support/troubleshooting/TSG01212623/)

## Testing status

GitHub Actions can verify that the Windows application and installer compile. **Actual firmware flashing has not been verified on physical devices.** Start by testing read-only detection, bootloader information and normal Fastboot reboot on a spare test device. Do not use the write operation on a customer's primary phone until the complete OEM-specific procedure is independently verified.
