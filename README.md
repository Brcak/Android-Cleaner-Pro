# Android Cleaner Pro — Windows technician application

Native C# / .NET 10 WPF Android technician tool with a dark dashboard, ADB device discovery, Android model/version information, third-party app inventory, conservative heuristic review, individually confirmed package removal and JSON reports.

## Windows build
Open [Windows .NET Build](https://github.com/Brcak/Android-Cleaner-Pro/actions/workflows/windows-dotnet.yml). Choose a **successful** run, download the `AndroidCleanerPro-Windows` artifact, extract it, and run `AndroidCleanerPro.exe`.

The experimental portable single-file/embedded-ADB approach has been removed. Install the [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) and Google's [Android SDK Platform Tools](https://developer.android.com/tools/releases/platform-tools). Set ADB on PATH or place `platform-tools` next to the EXE. Windows USB drivers may also be required.

This produces a standard Windows application folder, **not yet a Setup.exe or MSI installer**. See [Windows installation guide](docs/DOTNET_WINDOWS.md).

## Development
Install .NET 10 SDK on Windows and run `./build-dotnet-windows.ps1`. Build output: `dist/Windows/`.

## Safety and status
**Experimental; successful CI compilation and physical phone tests have not been verified.** Not an antivirus product. Package-name heuristics are not malware findings. Removing apps may erase data; obtain customer permission. Exported reports include device identifiers.

## Repository
- `src/AndroidCleanerPro/` — native WPF application
- `.github/workflows/windows-dotnet.yml` — normal Windows build
- `build-dotnet-windows.ps1` — publish script
- `docs/DOTNET_WINDOWS.md` — setup and troubleshooting

The obsolete Python prototype remains recoverable in Git history.
