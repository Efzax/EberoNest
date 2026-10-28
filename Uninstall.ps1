param(
    [string]$InstallDirectory = '',
    [string]$StartupDirectory = ''
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
$legacyExecutables = @(
    (Join-Path $env:LOCALAPPDATA 'Programs\Ebero DropNest\EberoDropNest.exe'),
    (Join-Path $env:LOCALAPPDATA 'Programs\DownloadsOrganizer\DownloadsOrganizer.exe')
)
$recognizedExecutables = @($installedExe) + $legacyExecutables
function Test-SameInstalledPayloadShortcutTarget([string]$TargetPath, [string]$Arguments, [string]$InstalledExePath) {
    if (-not [string]::Equals([IO.Path]::GetFileName($TargetPath), 'EberoNest.exe', [StringComparison]::OrdinalIgnoreCase) -or
        $Arguments -cne '--background' -or
        -not (Test-Path -LiteralPath $TargetPath -PathType Leaf) -or
        -not (Test-Path -LiteralPath $InstalledExePath -PathType Leaf)) { return $false }
    try {
        $targetHash = (Get-FileHash -LiteralPath $TargetPath -Algorithm SHA256 -ErrorAction Stop).Hash
        $installedHash = (Get-FileHash -LiteralPath $InstalledExePath -Algorithm SHA256 -ErrorAction Stop).Hash
        return [string]::Equals($targetHash, $installedHash, [StringComparison]::OrdinalIgnoreCase)
    }
    catch { return $false }
}

foreach ($process in @(Get-Process -Name @('EberoNest', 'EberoDropNest', 'DownloadsOrganizer') -ErrorAction SilentlyContinue)) {
    try { $processPath = $process.Path } catch { continue }
    if (-not $processPath -or -not ($recognizedExecutables | Where-Object { [string]::Equals([IO.Path]::GetFullPath($_), [IO.Path]::GetFullPath($processPath), [StringComparison]::OrdinalIgnoreCase) })) { continue }
    Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
    try { $process.WaitForExit(5000) } catch { }
}

$shell = New-Object -ComObject WScript.Shell
try {
    $ownedShortcuts = @{
        'EberoNest.lnk' = @($installedExe) + $legacyExecutables
        'Ebero DropNest.lnk' = @($legacyExecutables[0])
        'DownloadsOrganizer.lnk' = @($legacyExecutables[1])
    }
    foreach ($name in $ownedShortcuts.Keys) {
        $shortcutPath = Join-Path $StartupDirectory $name
        if (-not (Test-Path -LiteralPath $shortcutPath)) { continue }
        $shortcut = $shell.CreateShortcut($shortcutPath)
        $target = [string]$shortcut.TargetPath
        $matchesKnownTarget = $ownedShortcuts[$name] | Where-Object {
            $target -and [string]::Equals([IO.Path]::GetFullPath($target), [IO.Path]::GetFullPath($_), [StringComparison]::OrdinalIgnoreCase)
        }
        $argumentsRecognized = $shortcut.Arguments -ceq '--background' -or
            ($name -eq 'DownloadsOrganizer.lnk' -and [string]::IsNullOrEmpty([string]$shortcut.Arguments))
        $samePayloadAlias = $name -eq 'EberoNest.lnk' -and
            (Test-SameInstalledPayloadShortcutTarget $target ([string]$shortcut.Arguments) $installedExe)
        if (($matchesKnownTarget -and $argumentsRecognized) -or $samePayloadAlias) {
            Remove-Item -LiteralPath $shortcutPath -Force
        }
    }
}
finally {
    if ($shell) { [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell) }
}

foreach ($exe in $recognizedExecutables) {
    if (Test-Path -LiteralPath $exe -PathType Leaf) { Remove-Item -LiteralPath $exe -Force }
}
$cleanupDirectories = @($InstallDirectory) + @($legacyExecutables | ForEach-Object { Split-Path -Parent $_ })
foreach ($directory in ($cleanupDirectories | Select-Object -Unique)) {
    if ((Test-Path -LiteralPath $directory -PathType Container) -and -not (Get-ChildItem -LiteralPath $directory -Force | Select-Object -First 1)) {
        Remove-Item -LiteralPath $directory -Force
    }
}
Write-Output 'EberoNest y sus accesos reconocidos se eliminaron. Los archivos de reglas del usuario se conservaron.'

