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
    '#define MyAppVersion "1.0.0"',
    'DefaultDirName={localappdata}\Programs\EberoNest',
    'PrivilegesRequired=lowest',
    'SetupArchitecture=x64',
    'AppMutex=Local\DownloadsOrganizer.Instance',
    'CloseApplications=no',
    'UninstallDisplayIcon={app}\EberoNest.exe',
    'SetupIconFile=..\assets\app-icon.ico',
    'OutputBaseFilename=EberoNest-Setup',
    'Source: "..\dist\EberoNest.exe"; DestDir: "{app}"',
    'Name: "{userprograms}\EberoNest\EberoNest.lnk"',
    'Name: "{userstartup}\EberoNest.lnk"',
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
if ($iss -match '(?im)^\s*AppPublisher\s*=') { throw 'Installer verification failed: publisher was invented despite missing project metadata.' }
if ($iss -match '\{(?:auto|user|common)desktop\}') { throw 'Installer verification failed: a desktop shortcut is configured.' }
if ($iss -match '(?i)Invoke-WebRequest|DownloadFile|Start-BitsTransfer|winget|curl\.exe') { throw 'Installer verification failed: installer source contains a download mechanism.' }
$project = [xml](Get-Content -LiteralPath (Join-Path $projectRoot 'EberoNest.csproj') -Raw)
if ($project.Project.PropertyGroup.Product -cne 'EberoNest') { throw 'Installer verification failed: app product metadata differs.' }
if (-not (Test-Path -LiteralPath (Join-Path $projectRoot 'assets\app-icon.ico') -PathType Leaf)) { throw 'Installer verification failed: official app icon is missing.' }
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

