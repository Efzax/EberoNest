param(
    [string]$InnoSetupCompilerPath = ''
)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath($PSScriptRoot)
$buildScript = Join-Path $projectRoot 'Build.ps1'
$installerScript = Join-Path $projectRoot 'installer\EberoNest.iss'
$configurationVerification = Join-Path $projectRoot 'verification\InstallerConfiguration.Verification.ps1'
$initializationVerification = Join-Path $projectRoot 'verification\InstallerInitialization.Verification.ps1'
$smokeSetupDirectory = Join-Path $env:TEMP ('EberoNest-InstallerInit-' + [guid]::NewGuid().ToString('N'))
$smokeSetupPath = Join-Path $smokeSetupDirectory 'EberoNest-InstallerInit.exe'
$exePath = Join-Path $projectRoot 'dist\EberoNest.exe'
$setupPath = Join-Path $projectRoot 'release\EberoNest-Setup.exe'

if (-not (Test-Path -LiteralPath $installerScript -PathType Leaf)) { throw "No se encontró el script del instalador: $installerScript" }
& $buildScript
if ($LASTEXITCODE -ne 0) { throw 'Falló Build.ps1; no se continuará con el instalador.' }
& $configurationVerification
if ($LASTEXITCODE -ne 0) { throw 'Falló la verificación de configuración del instalador.' }

if ([string]::IsNullOrWhiteSpace($InnoSetupCompilerPath)) { $InnoSetupCompilerPath = $env:INNO_SETUP_COMPILER }
if ([string]::IsNullOrWhiteSpace($InnoSetupCompilerPath)) {
    $command = Get-Command 'ISCC.exe' -ErrorAction SilentlyContinue
    if ($command) { $InnoSetupCompilerPath = $command.Source }
}
if ([string]::IsNullOrWhiteSpace($InnoSetupCompilerPath)) {
    $candidates = @(
        (Join-Path $env:ProgramFiles 'Inno Setup 7\ISCC.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 7\ISCC.exe'),
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 7\ISCC.exe')
    ) | Where-Object { $_ -and (Test-Path -LiteralPath $_ -PathType Leaf) }
    if ($candidates.Count -gt 0) { $InnoSetupCompilerPath = $candidates[0] }
}
if ([string]::IsNullOrWhiteSpace($InnoSetupCompilerPath)) {
    throw 'No se encontró ISCC.exe de Inno Setup 7.1.0. Instala la herramienta oficial y vuelve a ejecutar Build-Installer.ps1; este script no descarga ni instala herramientas.'
}
if (-not (Test-Path -LiteralPath $InnoSetupCompilerPath -PathType Leaf)) {
    throw "No se encontró ISCC.exe en la ruta configurada: $InnoSetupCompilerPath"
}
$compilerVersionOutput = & $InnoSetupCompilerPath '--version' 2>&1
if ($LASTEXITCODE -ne 0) { throw "No se pudo consultar la versión de ISCC.exe: $compilerVersionOutput" }
$compilerVersion = ($compilerVersionOutput | Out-String).Trim()
if ($compilerVersion -ne '7.1.0') {
    throw "Se requiere Inno Setup 7.1.0 para este build reproducible; versión encontrada: $compilerVersion"
}

New-Item -ItemType Directory -Path (Split-Path -Parent $setupPath) -Force | Out-Null
Push-Location (Split-Path -Parent $installerScript)
try {
    $compilerOutput = & $InnoSetupCompilerPath '/Qp' $installerScript 2>&1
    $compilerExitCode = $LASTEXITCODE
    $compilerOutput | ForEach-Object { Write-Output $_ }
    if ($compilerExitCode -ne 0) { throw "ISCC.exe terminó con código $compilerExitCode." }
    if ($compilerOutput -match '(?im)^\s*Warning\s*:') { throw 'ISCC.exe informó warnings; se requiere corregirlos antes de entregar Setup.' }
    New-Item -ItemType Directory -Path $smokeSetupDirectory -Force | Out-Null
    $smokeCompilerOutput = & $InnoSetupCompilerPath '/Qp' '--define=InstallerInitSmoke=1' "--output-dir=$smokeSetupDirectory" '--output-filename=EberoNest-InstallerInit' $installerScript 2>&1
    $smokeCompilerExitCode = $LASTEXITCODE
    $smokeCompilerOutput | ForEach-Object { Write-Output $_ }
    if ($smokeCompilerExitCode -ne 0) { throw "ISCC.exe no pudo compilar el smoke Setup (código $smokeCompilerExitCode)." }
    if ($smokeCompilerOutput -match '(?im)^\s*Warning\s*:') { throw 'ISCC.exe informó warnings al compilar el smoke Setup.' }
}
finally { Pop-Location }

if (-not (Test-Path -LiteralPath $setupPath -PathType Leaf)) { throw "ISCC.exe no generó el artefacto esperado: $setupPath" }
& $initializationVerification -SmokeSetupPath $smokeSetupPath
if ($LASTEXITCODE -ne 0) { throw 'Falló la verificación de inicialización real del Setup.' }
Remove-Item -LiteralPath $smokeSetupDirectory -Recurse -Force
$setupInfo = (Get-Item -LiteralPath $setupPath).VersionInfo
$setupProductName = $setupInfo.ProductName.Trim()
$setupProductVersion = $setupInfo.ProductVersion.Trim()
if ($setupProductName -cne 'EberoNest' -or $setupProductVersion -notmatch '^1\.0\.2') {
    throw "Metadata incorrecta en el Setup: ProductName='$setupProductName', ProductVersion='$setupProductVersion'."
}
$exeHash = (Get-FileHash -LiteralPath $exePath -Algorithm SHA256).Hash
$setupHash = (Get-FileHash -LiteralPath $setupPath -Algorithm SHA256).Hash
[pscustomobject]@{
    Compiler = $InnoSetupCompilerPath
    CompilerVersion = $compilerVersion
    Setup = $setupPath
    SetupSizeBytes = (Get-Item -LiteralPath $setupPath).Length
    SetupSHA256 = $setupHash
    IncludedExecutable = $exePath
    IncludedExecutableSHA256 = $exeHash
} | Format-List
Write-Output 'PASS: EberoNest-Setup.exe compilado; Build, Verification y configuración del instalador pasaron.'


