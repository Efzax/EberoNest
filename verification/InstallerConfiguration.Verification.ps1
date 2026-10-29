$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$issPath = Join-Path $projectRoot 'installer\EberoNest.iss'
$exePath = Join-Path $projectRoot 'dist\EberoNest.exe'
$initializationTestPath = Join-Path $PSScriptRoot 'InstallerInitialization.Verification.ps1'
if (-not (Test-Path -LiteralPath $issPath -PathType Leaf)) { throw 'Installer verification failed: installer source is missing.' }
if (-not (Test-Path -LiteralPath $exePath -PathType Leaf)) { throw 'Installer verification failed: EberoNest.exe is missing.' }
if (-not (Test-Path -LiteralPath $initializationTestPath -PathType Leaf)) { throw 'Installer verification failed: runtime initialization test is missing.' }
$iss = [IO.File]::ReadAllText($issPath)
$required = @(
    'AppName={#MyAppName}',
    'AppVersion={#MyAppVersion}',
    '#define MyAppVersion "1.0.2"',
    'DefaultDirName={localappdata}\Programs\EberoNest',
    'PrivilegesRequired=lowest',
    'SetupArchitecture=x64',
    'AppMutex=Local\DownloadsOrganizer.Instance',
    'CloseApplications=no',
    'UninstallDisplayIcon={app}\EberoNest.exe',
    'SetupIconFile=..\assets\app-icon.ico',
    'OutputBaseFilename=EberoNest-Setup',
    'Source: "..\dist\EberoNest.exe"; DestDir: "{app}"',
    'Name: "{userprograms}\EberoNest\EberoNest"; Filename: "{app}\EberoNest.exe"; WorkingDir: "{app}"; IconFilename: "{app}\EberoNest.exe"',
    'Name: "{userstartup}\EberoNest"; Filename: "{app}\EberoNest.exe"; Parameters: "--background"; WorkingDir: "{app}"; IconFilename: "{app}\EberoNest.exe"',
    "StartMenuLink := ExpandConstant('{userprograms}\EberoNest\EberoNest.lnk');",
    "LinkPath := ExpandConstant('{userstartup}\EberoNest.lnk');",
    'RemoveRecognizedDoubleExtensionShortcuts',
    "ExpandConstant('{userprograms}\EberoNest\EberoNest.lnk.lnk')",
    "ExpandConstant('{userstartup}\EberoNest.lnk.lnk')",
    'Parameters: "--background"',
    'Description: "Ejecutar EberoNest"; Flags: postinstall nowait skipifsilent',
    'DropNestLegacyExe',
    'DownloadsOrganizerLegacyExe',
    'RemoveRecognizedLegacyShortcuts',
    'RemoveLegacyExecutables',
    'LinkMatchesCurrentPayload',
    'EberoNestPayloadSHA256',
    'GetSHA256OfFile',
    'CurUninstallStepChanged',
    "LinkMatches(LinkPath, InstalledExe, '--background') or LinkMatchesCurrentPayload(LinkPath, '--background')"
)
foreach ($text in $required) {
    if (-not $iss.Contains($text)) { throw "Installer verification failed: expected configuration is missing: $text" }
}
$iconSection = [regex]::Match($iss, '(?ms)^\[Icons\]\s*(.*?)^\[Run\]').Groups[1].Value
if ([string]::IsNullOrWhiteSpace($iconSection) -or $iconSection -match '(?im)^\s*Name:\s*"\{userprograms\}\\EberoNest\\EberoNest\.lnk"') {
    throw 'Installer verification failed: Start Menu Name must omit .lnk so Inno Setup creates the canonical EberoNest.lnk filename.'
}
if ($iconSection -match '(?im)^\s*Name:\s*"\{userstartup\}\\EberoNest\.lnk"') {
    throw 'Installer verification failed: Startup Name must omit .lnk to avoid generating EberoNest.lnk.lnk.'
}
if ($iconSection -match '(?im)^\s*Name:\s*"\{userprograms\}\\EberoNest\\EberoNest"[^\r\n]*Parameters:') {
    throw 'Installer verification failed: the Start Menu shortcut must launch interactively without arguments.'
}
$ownedAliasCleanup = [regex]::Match($iss, '(?ms)procedure RemoveRecognizedDoubleExtensionShortcuts\(\);(.*?)^end;').Groups[1].Value
foreach ($requiredGuard in @(
    "LinkMatches(LinkPath, InstalledExe, '')",
    "LinkMatchesCurrentPayload(LinkPath, '')",
    "LinkMatches(LinkPath, InstalledExe, '--background')",
    "LinkMatchesCurrentPayload(LinkPath, '--background')",
    "DeleteFile(LinkPath)"
)) {
    if (-not $ownedAliasCleanup.Contains($requiredGuard)) { throw "Installer verification failed: double-extension cleanup is missing ownership guard: $requiredGuard" }
}
if ($iss -match '(?im)^\s*AppPublisher\s*=') { throw 'Installer verification failed: publisher was invented despite missing project metadata.' }
if ($iss -match '\{(?:auto|user|common)desktop\}') { throw 'Installer verification failed: a desktop shortcut is configured.' }
if ($iss -match '(?i)Invoke-WebRequest|DownloadFile|Start-BitsTransfer|winget|curl\.exe') { throw 'Installer verification failed: installer source contains a download mechanism.' }
$project = [xml](Get-Content -LiteralPath (Join-Path $projectRoot 'EberoNest.csproj') -Raw)
if ($project.Project.PropertyGroup.Product -cne 'EberoNest') { throw 'Installer verification failed: app product metadata differs.' }
if ($project.Project.PropertyGroup.ApplicationIcon -cne 'assets\app-icon.ico') { throw 'Installer verification failed: the application executable must use assets\app-icon.ico.' }
if ($project.Project.PropertyGroup.Version -cne '1.0.2' -or $project.Project.PropertyGroup.AssemblyVersion -cne '1.0.2.0' -or
    $project.Project.PropertyGroup.FileVersion -cne '1.0.2.0' -or $project.Project.PropertyGroup.InformationalVersion -cne '1.0.2') {
    throw 'Installer verification failed: app version metadata is not 1.0.2.'
}
if (-not (Test-Path -LiteralPath (Join-Path $projectRoot 'assets\app-icon.ico') -PathType Leaf)) { throw 'Installer verification failed: official app icon is missing.' }
if (-not (Test-Path -LiteralPath (Join-Path $projectRoot 'assets\app-icon.png') -PathType Leaf)) { throw 'Installer verification failed: official large app logo is missing.' }
if ((Get-Item -LiteralPath $exePath).VersionInfo.ProductName -cne 'EberoNest') { throw 'Installer verification failed: payload metadata is not EberoNest.' }
if ((Get-Content -LiteralPath (Join-Path $projectRoot 'EberoNest.csproj') -Raw) -match '<PackageReference\b') { throw 'Installer verification failed: unexpected external application package dependency.' }
foreach ($scriptCheck in @(
    @{ Path = (Join-Path $projectRoot 'Install.ps1'); Required = @('Test-SameCurrentPayloadShortcutTarget', "Arguments -cne '--background'", 'Get-FileHash -LiteralPath $TargetPath -Algorithm SHA256') },
    @{ Path = (Join-Path $projectRoot 'Uninstall.ps1'); Required = @('Test-SameInstalledPayloadShortcutTarget', "Arguments -cne '--background'", 'Get-FileHash -LiteralPath $TargetPath -Algorithm SHA256') }
)) {
    $scriptText = [IO.File]::ReadAllText($scriptCheck.Path)
    foreach ($fragment in $scriptCheck.Required) {
        if (-not $scriptText.Contains($fragment)) { throw "Installer verification failed: payload identity check is missing from $($scriptCheck.Path): $fragment" }
    }
}
Write-Output 'PASS: installer identity, per-user target, AppMutex, startup, menu shortcut, post-install launch, payload, icon, uninstall cleanup, and no-download/no-publisher configuration.'

