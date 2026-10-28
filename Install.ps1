param(
    [string]$InstallDirectory = '',
    [string]$StartupDirectory = '',
    [switch]$NoLaunch
)
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($InstallDirectory)) {
    $InstallDirectory = Join-Path $env:LOCALAPPDATA 'Programs\EberoNest'
}
if ([string]::IsNullOrWhiteSpace($StartupDirectory)) {
    $StartupDirectory = [Environment]::GetFolderPath('Startup')
}
$InstallDirectory = [System.IO.Path]::GetFullPath($InstallDirectory)
$StartupDirectory = [System.IO.Path]::GetFullPath($StartupDirectory)
$installedExe = Join-Path $InstallDirectory 'EberoNest.exe'
$sourceExe = Join-Path $PSScriptRoot 'dist\EberoNest.exe'
$legacyExecutables = @(
    (Join-Path $env:LOCALAPPDATA 'Programs\Ebero DropNest\EberoDropNest.exe'),
    (Join-Path $env:LOCALAPPDATA 'Programs\DownloadsOrganizer\DownloadsOrganizer.exe')
)
$recognizedExecutables = @($installedExe) + $legacyExecutables
function Test-SameCurrentPayloadShortcutTarget([string]$TargetPath, [string]$Arguments, [string]$PayloadPath) {
    if (-not [string]::Equals([IO.Path]::GetFileName($TargetPath), 'EberoNest.exe', [StringComparison]::OrdinalIgnoreCase) -or
        $Arguments -cne '--background' -or
        -not (Test-Path -LiteralPath $TargetPath -PathType Leaf) -or
        -not (Test-Path -LiteralPath $PayloadPath -PathType Leaf)) { return $false }
    try {
        $targetHash = (Get-FileHash -LiteralPath $TargetPath -Algorithm SHA256 -ErrorAction Stop).Hash
        $payloadHash = (Get-FileHash -LiteralPath $PayloadPath -Algorithm SHA256 -ErrorAction Stop).Hash
        return [string]::Equals($targetHash, $payloadHash, [StringComparison]::OrdinalIgnoreCase)
    }
    catch { return $false }
}
if (-not (Test-Path -LiteralPath $sourceExe -PathType Leaf)) {
    throw "No se encontró el ejecutable del paquete: $sourceExe"
}

foreach ($process in @(Get-Process -Name @('EberoNest', 'EberoDropNest', 'DownloadsOrganizer') -ErrorAction SilentlyContinue)) {
    try { $processPath = $process.Path } catch { continue }
    if (-not $processPath -or -not ($recognizedExecutables | Where-Object { [string]::Equals([IO.Path]::GetFullPath($_), [IO.Path]::GetFullPath($processPath), [StringComparison]::OrdinalIgnoreCase) })) { continue }
    try { $process.CloseMainWindow() | Out-Null } catch { }
    try { $process.WaitForExit(3000) } catch { }
    if (-not $process.HasExited) {
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        try { $process.WaitForExit(5000) } catch { }
    }
}

New-Item -ItemType Directory -Path $InstallDirectory -Force | Out-Null
Copy-Item -LiteralPath $sourceExe -Destination $installedExe -Force
New-Item -ItemType Directory -Path $StartupDirectory -Force | Out-Null
$shortcutPath = Join-Path $StartupDirectory 'EberoNest.lnk'
$legacyShortcutTargets = @{
    'Ebero DropNest.lnk' = $legacyExecutables[0]
    'DownloadsOrganizer.lnk' = $legacyExecutables[1]
}
$shell = New-Object -ComObject WScript.Shell
try {
    if (Test-Path -LiteralPath $shortcutPath) {
        $existing = $shell.CreateShortcut($shortcutPath)
        $target = [IO.Path]::GetFullPath([string]$existing.TargetPath)
        $canonicalTarget = [IO.Path]::GetFullPath($installedExe)
        $recognized = [string]::Equals($target, $canonicalTarget, [StringComparison]::OrdinalIgnoreCase) -or
            ($legacyExecutables | Where-Object { [string]::Equals($target, [IO.Path]::GetFullPath($_), [StringComparison]::OrdinalIgnoreCase) }).Count -gt 0 -or
            (Test-SameCurrentPayloadShortcutTarget ([string]$existing.TargetPath) ([string]$existing.Arguments) $sourceExe)
        if (-not $recognized -or $existing.Arguments -cne '--background') {
            throw 'El acceso EberoNest.lnk existente no coincide con una instalación EberoNest reconocida y --background; se conservó sin cambios.'
        }
    }

    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = $installedExe
    $shortcut.WorkingDirectory = $InstallDirectory
    $shortcut.Arguments = '--background'
    $shortcut.Description = 'EberoNest permanece en la bandeja del sistema'
    $shortcut.Save()

    foreach ($legacyName in $legacyShortcutTargets.Keys) {
        $legacyPath = Join-Path $StartupDirectory $legacyName
        if (-not (Test-Path -LiteralPath $legacyPath)) { continue }
        $legacyShortcut = $shell.CreateShortcut($legacyPath)
        $legacyTarget = [string]$legacyShortcut.TargetPath
        $expectedTarget = [string]$legacyShortcutTargets[$legacyName]
        $argumentsRecognized = $legacyShortcut.Arguments -ceq '--background' -or
            ($legacyName -eq 'DownloadsOrganizer.lnk' -and [string]::IsNullOrEmpty([string]$legacyShortcut.Arguments))
        if ([string]::Equals([IO.Path]::GetFullPath($legacyTarget), [IO.Path]::GetFullPath($expectedTarget), [StringComparison]::OrdinalIgnoreCase) -and $argumentsRecognized) {
            Remove-Item -LiteralPath $legacyPath -Force
        }
    }
}
finally {
    if ($shell) { [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell) }
}

foreach ($legacyExe in $legacyExecutables) {
    if (Test-Path -LiteralPath $legacyExe -PathType Leaf) { Remove-Item -LiteralPath $legacyExe -Force }
    $legacyDirectory = Split-Path -Parent $legacyExe
    if ((Test-Path -LiteralPath $legacyDirectory -PathType Container) -and -not (Get-ChildItem -LiteralPath $legacyDirectory -Force | Select-Object -First 1)) {
        Remove-Item -LiteralPath $legacyDirectory -Force
    }
}

if (-not $NoLaunch) {
    Start-Process -FilePath $installedExe -ArgumentList '--background' -WorkingDirectory $InstallDirectory -WindowStyle Hidden
}
Write-Output "EberoNest instalado en $InstallDirectory; inicio automático configurado."
if (-not $NoLaunch) { Write-Output 'EberoNest ya está iniciado en la bandeja del sistema.' }

