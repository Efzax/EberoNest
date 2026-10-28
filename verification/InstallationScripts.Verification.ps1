$ErrorActionPreference = 'Stop'
$root = Join-Path ([IO.Path]::GetTempPath()) ('EberoNest-install-check-' + [guid]::NewGuid().ToString('N'))
$local = Join-Path $root 'local'
$startup = Join-Path $root 'startup'
$projectRoot = Split-Path -Parent $PSScriptRoot
$sourceExe = Join-Path $projectRoot 'dist\EberoNest.exe'
$developmentExe = Join-Path $root 'development\EberoNest.exe'
$foreignEberoExe = Join-Path $root 'foreign\EberoNest.exe'
$installScript = Join-Path $projectRoot 'Install.ps1'
$uninstallScript = Join-Path $projectRoot 'Uninstall.ps1'
$oldLocalAppData = $env:LOCALAPPDATA
New-Item -ItemType Directory -Path $local,$startup,(Split-Path -Parent $developmentExe),(Split-Path -Parent $foreignEberoExe) -Force | Out-Null
Copy-Item -LiteralPath $sourceExe -Destination $developmentExe
$env:LOCALAPPDATA = $local
try {
    $canonicalDirectory = Join-Path $local 'Programs\EberoNest'
    $canonicalExe = Join-Path $canonicalDirectory 'EberoNest.exe'
    $dropNestExe = Join-Path $local 'Programs\Ebero DropNest\EberoDropNest.exe'
    $downloadsExe = Join-Path $local 'Programs\DownloadsOrganizer\DownloadsOrganizer.exe'
    New-Item -ItemType Directory -Path (Split-Path -Parent $dropNestExe),(Split-Path -Parent $downloadsExe) -Force | Out-Null
    [IO.File]::WriteAllBytes($dropNestExe, [byte[]](1,2,3))
    [IO.File]::WriteAllBytes($foreignEberoExe, [byte[]](9,8,7,6))
    [IO.File]::WriteAllBytes($downloadsExe, [byte[]](4,5,6))
    $rulesOne = Join-Path $local 'Ebero DropNest\rules.json'
    $rulesTwo = Join-Path $local 'DownloadsOrganizer\rules.json'
    New-Item -ItemType Directory -Path (Split-Path -Parent $rulesOne),(Split-Path -Parent $rulesTwo) -Force | Out-Null
    [IO.File]::WriteAllText($rulesOne, '{"legacy":"dropnest"}')
    [IO.File]::WriteAllText($rulesTwo, '{"legacy":"downloads"}')

    $shell = New-Object -ComObject WScript.Shell
    try {
        foreach ($record in @(
            @{ Name = 'Ebero DropNest.lnk'; Target = $dropNestExe; Arguments = '--background' },
            @{ Name = 'DownloadsOrganizer.lnk'; Target = $downloadsExe; Arguments = '' },
            @{ Name = 'Foreign.lnk'; Target = (Join-Path $root 'OtherApp.exe'); Arguments = '--background' }
        )) {
            $link = $shell.CreateShortcut((Join-Path $startup $record.Name))
            $link.TargetPath = $record.Target
            $link.Arguments = $record.Arguments
            $link.Save()
        }
        $ambiguous = $shell.CreateShortcut((Join-Path $startup 'Ambiguous Ebero DropNest.lnk'))
        $ambiguous.TargetPath = (Join-Path $root 'OtherApp.exe')
        $ambiguous.Arguments = '--background'
        $ambiguous.Save()
    }
    finally { [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell) }

    & $installScript -StartupDirectory $startup -NoLaunch
    if (-not (Test-Path -LiteralPath $canonicalExe) -or (Get-FileHash -LiteralPath $canonicalExe -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath $sourceExe -Algorithm SHA256).Hash) { throw 'Install verification failed: canonical EberoNest.exe was not installed byte-for-byte.' }
    if ((Test-Path -LiteralPath $dropNestExe) -or (Test-Path -LiteralPath $downloadsExe)) { throw 'Install verification failed: recognized legacy executables were not removed.' }
    if (-not (Test-Path -LiteralPath $rulesOne) -or -not (Test-Path -LiteralPath $rulesTwo)) { throw 'Install verification failed: legacy rules files were changed or removed.' }
    foreach ($legacyName in @('Ebero DropNest.lnk','DownloadsOrganizer.lnk')) { if (Test-Path -LiteralPath (Join-Path $startup $legacyName)) { throw "Install verification failed: recognized legacy shortcut remains: $legacyName" } }
    if (-not (Test-Path -LiteralPath (Join-Path $startup 'Foreign.lnk')) -or -not (Test-Path -LiteralPath (Join-Path $startup 'Ambiguous Ebero DropNest.lnk'))) { throw 'Install verification failed: unrelated or ambiguous shortcuts were removed.' }
    $shell = New-Object -ComObject WScript.Shell
    try {
        $ambiguousLegacy = $shell.CreateShortcut((Join-Path $startup 'Ebero DropNest.lnk'))
        $ambiguousLegacy.TargetPath = (Join-Path $root 'OtherApp.exe')
        $ambiguousLegacy.Arguments = '--background'
        $ambiguousLegacy.Save()
    }
    finally { [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell) }
    & $installScript -StartupDirectory $startup -NoLaunch

    $shell = New-Object -ComObject WScript.Shell
    try {
        $canonicalLink = $shell.CreateShortcut((Join-Path $startup 'EberoNest.lnk'))
        if ([IO.Path]::GetFullPath($canonicalLink.TargetPath) -ne [IO.Path]::GetFullPath($canonicalExe) -or $canonicalLink.Arguments -cne '--background') { throw 'Install verification failed: canonical shortcut target or arguments are incorrect.' }
    }
    finally { [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell) }

    $shell = New-Object -ComObject WScript.Shell
    try {
        $foreignCanonical = $shell.CreateShortcut((Join-Path $startup 'EberoNest.lnk'))
        $foreignCanonical.TargetPath = $foreignEberoExe
        $foreignCanonical.Arguments = '--background'
        $foreignCanonical.Save()
    }
    finally { [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell) }
    $foreignRejected = $false
    try { & $installScript -StartupDirectory $startup -NoLaunch } catch { $foreignRejected = $true }
    if (-not $foreignRejected) { throw 'Install verification failed: a foreign EberoNest.exe payload was accepted.' }
    $shell = New-Object -ComObject WScript.Shell
    try {
        $preservedForeign = $shell.CreateShortcut((Join-Path $startup 'EberoNest.lnk'))
        if ([IO.Path]::GetFullPath($preservedForeign.TargetPath) -ne [IO.Path]::GetFullPath($foreignEberoExe)) { throw 'Install verification failed: foreign EberoNest shortcut was modified.' }
        $wrongArgsLink = $shell.CreateShortcut((Join-Path $startup 'EberoNest.lnk'))
        $wrongArgsLink.TargetPath = $developmentExe
        $wrongArgsLink.Arguments = '--wrong'
        $wrongArgsLink.Save()
    }
    finally { [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell) }
    $wrongArgumentsRejected = $false
    try { & $installScript -StartupDirectory $startup -NoLaunch } catch { $wrongArgumentsRejected = $true }
    if (-not $wrongArgumentsRejected) { throw 'Install verification failed: a same-payload shortcut with incorrect arguments was accepted.' }
    $shell = New-Object -ComObject WScript.Shell
    try {
        $preservedWrongArgs = $shell.CreateShortcut((Join-Path $startup 'EberoNest.lnk'))
        if ([IO.Path]::GetFullPath($preservedWrongArgs.TargetPath) -ne [IO.Path]::GetFullPath($developmentExe) -or $preservedWrongArgs.Arguments -cne '--wrong') { throw 'Install verification failed: same-payload shortcut with incorrect arguments was modified.' }
        $adoptionLink = $shell.CreateShortcut((Join-Path $startup 'EberoNest.lnk'))
        $adoptionLink.TargetPath = $developmentExe
        $adoptionLink.Arguments = '--background'
        $adoptionLink.Save()
    }
    finally { [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell) }
    & $installScript -StartupDirectory $startup -NoLaunch

    $shell = New-Object -ComObject WScript.Shell
    try {
        $legacyNoArguments = $shell.CreateShortcut((Join-Path $startup 'DownloadsOrganizer.lnk'))
        $legacyNoArguments.TargetPath = $downloadsExe
        $legacyNoArguments.Arguments = ''
        $legacyNoArguments.Save()
        [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($legacyNoArguments)
    }
    finally { [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell) }
    $shell = New-Object -ComObject WScript.Shell
    try {
        $developmentAlias = $shell.CreateShortcut((Join-Path $startup 'EberoNest.lnk'))
        $developmentAlias.TargetPath = $developmentExe
        $developmentAlias.Arguments = '--background'
        $developmentAlias.Save()
    }
    finally { [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell) }
    & $uninstallScript -InstallDirectory $canonicalDirectory -StartupDirectory $startup
    if (-not (Test-Path -LiteralPath $developmentExe -PathType Leaf)) { throw 'Uninstall verification failed: external same-payload development EXE was deleted.' }
    if (Test-Path -LiteralPath (Join-Path $startup 'DownloadsOrganizer.lnk')) { throw 'Uninstall verification failed: recognized no-argument DownloadsOrganizer shortcut remains.' }
    if (Test-Path -LiteralPath $canonicalExe) { throw 'Uninstall verification failed: canonical executable remains.' }
    if (Test-Path -LiteralPath (Join-Path $startup 'EberoNest.lnk')) { throw 'Uninstall verification failed: owned canonical shortcut remains.' }
    if (-not (Test-Path -LiteralPath $rulesOne) -or -not (Test-Path -LiteralPath $rulesTwo)) { throw 'Uninstall verification failed: user rules files were removed.' }
    if (-not (Test-Path -LiteralPath (Join-Path $startup 'Foreign.lnk')) -or -not (Test-Path -LiteralPath (Join-Path $startup 'Ambiguous Ebero DropNest.lnk'))) { throw 'Uninstall verification failed: unrelated or ambiguous shortcuts were removed.' }
    New-Item -ItemType Directory -Path $canonicalDirectory -Force | Out-Null
    Copy-Item -LiteralPath $sourceExe -Destination $canonicalExe -Force
    $shell = New-Object -ComObject WScript.Shell
    try {
        $foreignForUninstall = $shell.CreateShortcut((Join-Path $startup 'EberoNest.lnk'))
        $foreignForUninstall.TargetPath = $foreignEberoExe
        $foreignForUninstall.Arguments = '--background'
        $foreignForUninstall.Save()
    }
    finally { [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell) }
    & $uninstallScript -InstallDirectory $canonicalDirectory -StartupDirectory $startup
    if (-not (Test-Path -LiteralPath (Join-Path $startup 'EberoNest.lnk'))) { throw 'Uninstall verification failed: a foreign same-name shortcut was removed.' }
    $shell = New-Object -ComObject WScript.Shell
    try {
        $preservedForeignUninstall = $shell.CreateShortcut((Join-Path $startup 'EberoNest.lnk'))
        if ([IO.Path]::GetFullPath($preservedForeignUninstall.TargetPath) -ne [IO.Path]::GetFullPath($foreignEberoExe)) { throw 'Uninstall verification failed: a foreign same-name shortcut was modified.' }
    }
    finally { [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell) }
    Write-Output 'PASS: isolated Install/Uninstall, same-payload development shortcut adoption, foreign/wrong-argument preservation, legacy cleanup, and rules preservation.'
}
finally {
    $env:LOCALAPPDATA = $oldLocalAppData
    if (Test-Path -LiteralPath $root) { Remove-Item -LiteralPath $root -Recurse -Force }
}


