# EberoNest

**Your downloads, automatically sorted.**

EberoNest is a Windows application by [Efzax](https://github.com/Efzax). See the [official EberoNest repository](https://github.com/Efzax/EberoNest) for source code and releases.

EberoNest organizes new files in your Downloads folder by extension. It runs in the Windows system tray and lets you configure categories, destination folders, and file extensions. You can organize eligible files already in the Downloads root from the app. Startup with Windows can be enabled or disabled in Settings.

## Requirements

- Windows x64
- For building from source: .NET 10 SDK (the project is validated with SDK 10.0.403)
- For building the installer: Inno Setup 7.1.0

End users do not need to install .NET; the application is published as a self-contained executable.

## Install

Download `EberoNest-Setup.exe` from the [official GitHub Releases page](https://github.com/Efzax/EberoNest/releases) and run it. Setup installs per-user under `%LOCALAPPDATA%\Programs\EberoNest`, creates a Start Menu shortcut, and enables background startup by default. The startup option can be changed later in Settings.

The app’s rules are stored locally at `%LOCALAPPDATA%\EberoNest\rules.json`. During an upgrade, rules from a previous installation are imported when no EberoNest rules file exists. Uninstalling preserves user rules.

## Privacy and security

EberoNest works locally on your computer and has no telemetry or analytics. Starting with version 1.0.1, it can check the latest stable release from the official `Efzax/EberoNest` GitHub repository over HTTPS. Automatic checks run at most once per day after a successful check; users can also check manually from About. The request sends the app name and version in its standard User-Agent, along with normal connection metadata required by GitHub. It does not send the Windows username, file paths, rules, files, Downloads contents, configured extensions, or a device identifier. EberoNest does not download or execute release assets and does not update or install itself. It watches only the top level of Downloads and moves matching regular files into configured category folders. Unknown extensions, temporary download files, and folders remain in Downloads. Configured destination folders that are Windows reparse points are rejected.

The interface supports Spanish and English. Its language preference is stored separately from rules at `%LOCALAPPDATA%\EberoNest\language.txt`; when no valid preference exists, the Windows display language selects the initial language.

Version 1.0.2 is not digitally signed. Windows SmartScreen may show an unknown publisher warning when you run the installer.

## Build from source

Open PowerShell in the repository folder and run:

```powershell
./Build.ps1
```

This publishes `dist/EberoNest.exe`, runs functional Verification with warnings treated as errors, performs the UI smoke test, and exercises install/uninstall scripts in an isolated temporary directory.

To compile `release/EberoNest-Setup.exe` as well, install Inno Setup 7.1.0 and run:

```powershell
./Build-Installer.ps1
```

The installer build includes configuration and pre-install initialization checks. The scripts use paths relative to the repository and work from any checkout location.

## Contributing

Contributions should preserve the local-first behavior, file safety checks, and existing Verification coverage. Build and run Verification before submitting changes.

## License

Copyright (c) 2026 Efzax. EberoNest is licensed under the MIT License. See [LICENSE](LICENSE).
