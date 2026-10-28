# EberoNest

**Your downloads, automatically sorted.**

EberoNest is a Windows application by Efzax.

EberoNest organizes new files in your Downloads folder by extension. It runs in the Windows system tray and lets you configure categories, destination folders, and file extensions. You can organize eligible files already in the Downloads root from the app. Startup with Windows can be enabled or disabled in Settings.

## Requirements

- Windows x64
- For building from source: .NET 10 SDK (the project is validated with SDK 10.0.403)
- For building the installer: Inno Setup 7.1.0

End users do not need to install .NET; the application is published as a self-contained executable.

## Install

Download `EberoNest-Setup.exe` from the project’s GitHub Releases page and run it. Setup installs per-user under `%LOCALAPPDATA%\Programs\EberoNest`, creates a Start Menu shortcut, and enables background startup by default. The startup option can be changed later in Settings.

The app’s rules are stored locally at `%LOCALAPPDATA%\EberoNest\rules.json`. During an upgrade, rules from a previous installation are imported when no EberoNest rules file exists. Uninstalling preserves user rules.

## Privacy and security

EberoNest works locally on your computer. Version 1.0.0 has no telemetry, does not send files or rules to the Internet, and does not execute downloaded files. It watches only the top level of Downloads and moves matching regular files into configured category folders. Unknown extensions, temporary download files, and folders remain in Downloads. Configured destination folders that are Windows reparse points are rejected.

Version 1.0.0 is not digitally signed. Windows SmartScreen may show an unknown publisher warning when you run the installer.

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
