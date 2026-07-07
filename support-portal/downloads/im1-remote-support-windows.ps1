$ErrorActionPreference = 'Stop'

$clientDownloadUrl = 'https://github.com/rustdesk/rustdesk/releases/download/1.4.9/rustdesk-1.4.9-x86_64.exe'
$supportHost = 'support.im1os.com'
$serverKey = '0t5yzcyqDFe2yj4X50zdRemrGtotzXD2Pxj8P4b43zI='

$workDir = Join-Path $env:TEMP 'im1-remote-support'
$clientPath = Join-Path $workDir 'iM1-Remote-Support.exe'
$configDir = Join-Path $env:APPDATA 'RustDesk\config'
$configPath = Join-Path $configDir 'RustDesk2.toml'
$installedClientPaths = @(
    "$env:ProgramFiles\RustDesk\rustdesk.exe",
    "${env:ProgramFiles(x86)}\RustDesk\rustdesk.exe"
) | Where-Object { $_ -and (Test-Path $_) }

Write-Host ''
Write-Host 'iM1 Remote Support'
Write-Host 'Downloading the Remote Support application...'

New-Item -ItemType Directory -Force -Path $workDir | Out-Null
New-Item -ItemType Directory -Force -Path $configDir | Out-Null

[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
Invoke-WebRequest -Uri $clientDownloadUrl -OutFile $clientPath -UseBasicParsing

$config = @"
rendezvous_server = '$supportHost'
nat_type = 1
serial = 0

[options]
custom-rendezvous-server = '$supportHost'
relay-server = '$supportHost'
key = '$serverKey'
"@

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($configPath, $config, $utf8NoBom)

Write-Host 'Applying iM1 Remote Support server settings...'
$launchPath = if ($installedClientPaths.Count -gt 0) { $installedClientPaths[0] } else { $clientPath }

try {
    $importProcess = Start-Process -FilePath $launchPath -ArgumentList @('--import-config', $configPath) -PassThru
    if (-not $importProcess.WaitForExit(5000)) {
        $importProcess.CloseMainWindow() | Out-Null
    }
} catch {
    Write-Host 'Continuing after direct config write.'
}

Start-Sleep -Seconds 1
Start-Process -FilePath $launchPath

Write-Host ''
Write-Host 'Remote Support is opening now.'
Write-Host 'Provide your Support ID to your iM1 technician, then click Allow when prompted.'
