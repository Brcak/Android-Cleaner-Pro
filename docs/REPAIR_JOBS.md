# Repair Jobs and Customer Reports

Open **Tools → Repair Job Records**, the **Repair Jobs** toolbar shortcut, or the Reports section.

The window stores technician-entered repair tickets in a local SQLite database:
`%LOCALAPPDATA%\AndroidCleanerPro\repair-cases.sqlite3`

Each ticket includes a reference, device model, customer's reported issue, diagnosis and evidence, work performed, and final result. Existing cases can be selected and updated. There is no automatic collection of phone serials, IMEIs, or personal customer data.

**Export Customer Report (.html)** creates an escaped, printable report with the before/after service notes. Open it in a browser and use **Print → Save as PDF** if a PDF is needed. All report fields come from the technician's notes and are not automatically verified as fact.

The app does not automatically attach Hazard Database entries or Android logs to a customer record; copying reviewed summaries is a manual decision to reduce accidental disclosure. Reports can contain sensitive free-text notes, so review them before sharing. This is not an invoicing or customer account management system.

Back up the SQLite file while Android Cleaner Pro is closed when moving computers. The installer does not delete this per-user database.
