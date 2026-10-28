param(
    [Parameter(Mandatory = $true)]
    [string]$SmokeSetupPath
)
$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $SmokeSetupPath -PathType Leaf)) { throw "Installer initialization verification failed: smoke Setup is missing: $SmokeSetupPath" }
$logPath = Join-Path $env:TEMP ('EberoNest-InstallerInit-' + [guid]::NewGuid().ToString('N') + '.log')
$arguments = @('/VERYSILENT', '/SUPPRESSMSGBOXES', "/LOG=$logPath")
$process = Start-Process -FilePath $SmokeSetupPath -ArgumentList $arguments -PassThru
try {
    if (-not $process.WaitForExit(20000)) {
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        throw 'Installer initialization verification failed: smoke Setup did not abort promptly.'
    }
    $logText = if (Test-Path -LiteralPath $logPath) { Get-Content -LiteralPath $logPath -Raw } else { '' }
    if ($logText -match '(?i)internal error|runtime error|attempt was made to expand.*app.*constant|expand.*app.*constant.*initialized') {
        throw "Installer initialization verification failed: runtime error found in Setup log: $logText"
    }
    if ($process.ExitCode -ne 0 -and $logText -notmatch 'InitializeSetup returned False; aborting\.') {
        throw "Installer initialization verification failed: unexpected Setup exit code $($process.ExitCode). Log: $logText"
    }
    if ($logText -notmatch 'InitializeSetup returned False; aborting\.') {
        throw "Installer initialization verification failed: smoke Setup did not reach its intentional pre-install abort. Log: $logText"
    }
    Write-Output 'PASS: compiled initialization-smoke Setup evaluated every early path helper and logged its intentional abort before installation.'
}
finally {
    Remove-Item -LiteralPath $logPath -Force -ErrorAction SilentlyContinue
}

