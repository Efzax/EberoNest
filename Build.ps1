$ErrorActionPreference = 'Stop'

$projectRoot = [IO.Path]::GetFullPath($PSScriptRoot)
$project = Join-Path $projectRoot 'EberoNest.csproj'
$verificationProject = Join-Path $projectRoot 'verification\EberoNest.Verification.csproj'
$distDirectory = Join-Path $projectRoot 'dist'
$packageDirectory = Join-Path $projectRoot 'EberoNest-Instalacion'
$exePath = Join-Path $distDirectory 'EberoNest.exe'

# Clear only generated output folders for the application and verification projects.
foreach ($generatedPath in @((Join-Path $projectRoot 'bin'),(Join-Path $projectRoot 'obj'),(Join-Path $projectRoot 'verification\bin'),(Join-Path $projectRoot 'verification\obj'),$packageDirectory)) {
    $resolvedGenerated = [IO.Path]::GetFullPath($generatedPath)
    if (-not $resolvedGenerated.StartsWith($projectRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw "Ruta de limpieza fuera del repositorio: $resolvedGenerated" }
    if (Test-Path -LiteralPath $resolvedGenerated) { Remove-Item -LiteralPath $resolvedGenerated -Recurse -Force }
}

$publishArgs = @('publish', $project, '-c', 'Release', '-r', 'win-x64', '--self-contained', 'true', '-p:PublishSingleFile=true', '-p:IncludeNativeLibrariesForSelfExtract=true', '-p:EnableCompressionInSingleFile=true', '-p:DebugType=None', '-p:DebugSymbols=false', '-warnaserror', '-o', $distDirectory)
& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) { throw 'Falló la compilación del ejecutable EberoNest.' }

$verificationArgs = @('run', '--project', $verificationProject, '-c', 'Release', '-p:TreatWarningsAsErrors=true')
& dotnet @verificationArgs
if ($LASTEXITCODE -ne 0) { throw 'Falló la verificación EberoNest.' }

& (Join-Path $projectRoot 'verification\InstallationScripts.Verification.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Falló la verificación aislada de instalación/desinstalación.' }

foreach ($obsoleteArtifact in @('EberoDropNest.exe','DownloadsOrganizer.exe')) {
    $oldPath = Join-Path $distDirectory $obsoleteArtifact
    if (Test-Path -LiteralPath $oldPath) { Remove-Item -LiteralPath $oldPath -Force }
    $oldPath = Join-Path $packageDirectory ('dist\' + $obsoleteArtifact)
    if (Test-Path -LiteralPath $oldPath) { Remove-Item -LiteralPath $oldPath -Force }
}
if (-not (Test-Path -LiteralPath $exePath -PathType Leaf)) { throw 'No se generó dist\EberoNest.exe.' }
$projectXml = [xml](Get-Content -LiteralPath $project -Raw)
foreach ($propertyName in @('AssemblyName','RootNamespace','Product','AssemblyTitle')) {
    if ($projectXml.Project.PropertyGroup.$propertyName -cne 'EberoNest') { throw "Metadata de producto incorrecta: $propertyName" }
}
$versionInfo = (Get-Item -LiteralPath $exePath).VersionInfo
if ($versionInfo.ProductName -cne 'EberoNest' -or [string]::IsNullOrWhiteSpace($versionInfo.ProductVersion)) { throw 'Los metadatos de producto/version del EXE no corresponden a EberoNest.' }
$exeHash = (Get-FileHash -LiteralPath $exePath -Algorithm SHA256).Hash
$verificationText = @"
EberoNest v1.0.0 — Build and verification

PASS — Published dist/EberoNest.exe for Windows x64 with warnings treated as errors.
PASS — Functional verification and UI smoke tests completed.
PASS — Isolated install/uninstall tests completed.

EXE: dist/EberoNest.exe
SHA-256: $exeHash
"@
Set-Content -LiteralPath (Join-Path $projectRoot 'RESULTADO-PRUEBAS.txt') -Value $verificationText -Encoding utf8

$packageExe = Join-Path $packageDirectory 'dist\EberoNest.exe'
New-Item -ItemType Directory -Path (Split-Path -Parent $packageExe) -Force | Out-Null
Copy-Item -LiteralPath $exePath -Destination $packageExe -Force
foreach ($fileName in @('Install.ps1','Uninstall.ps1','RESULTADO-PRUEBAS.txt')) {
    Copy-Item -LiteralPath (Join-Path $projectRoot $fileName) -Destination $packageDirectory -Force
}
$zipPath = Join-Path $projectRoot 'EberoNest-Instalacion.zip'
Compress-Archive -Path (Join-Path $packageDirectory '*') -DestinationPath $zipPath -Force
Write-Output "PASS: Build and verification completed; SHA-256 $exeHash"
