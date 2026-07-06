<#
.SYNOPSIS
Quick Dev deployment for validating local working changes without committing.

.DESCRIPTION
Deploy Dev is intentionally lighter than Deploy Platform. It publishes the affected
service outputs, pushes them to the Dev server, restarts the selected services, and
checks health. It does not commit or push Git changes.

If risky changes are detected, the script stops with a warning unless
-AcknowledgeRisk is supplied. Risky changes include migrations, EF model changes,
startup/config/security changes, and dependency/build graph changes.

.EXAMPLE
.\deploy\deploy-dev.ps1

.EXAMPLE
.\deploy\deploy-dev.ps1 -Services web -TestFilter "MerchantAccountServiceTests"

.EXAMPLE
.\deploy\deploy-dev.ps1 -AcknowledgeRisk
#>
[CmdletBinding()]
param(
    [string[]]$Services = @("auto"),
    [string]$Name = "",
    [string]$DevHost = "im1admin@im1-dev.im1os.com",
    [string]$HealthUrl = "http://127.0.0.1:5080/health",
    [string]$PublicHealthUrl = "https://dev.im1os.com/health",
    [string]$KnownHostsFile = "",
    [switch]$AcknowledgeRisk,
    [switch]$SkipBuild,
    [switch]$SkipTests,
    [switch]$RunTests,
    [string]$TestFilter = "",
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

function Write-Step {
    param([string]$Message)
    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Invoke-External {
    param(
        [string]$FilePath,
        [string[]]$Arguments
    )

    Write-Host ("$FilePath {0}" -f ($Arguments -join " ")) -ForegroundColor DarkGray
    if ($DryRun) {
        return
    }

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code ${LASTEXITCODE}: $FilePath"
    }
}

function Get-ChangedFiles {
    $tracked = @(git diff --name-only HEAD)
    $untracked = @(git ls-files --others --exclude-standard)
    return @($tracked + $untracked | Where-Object { $_ } | Sort-Object -Unique)
}

function Resolve-Services {
    param([string[]]$RequestedServices, [string[]]$ChangedFiles)

    if ($RequestedServices.Count -gt 0 -and $RequestedServices[0] -ne "auto") {
        return @($RequestedServices | Sort-Object -Unique)
    }

    $resolved = New-Object System.Collections.Generic.List[string]
    foreach ($file in $ChangedFiles) {
        $normalized = $file.Replace("\", "/")
        if ($normalized -like "src/iM1os.Web/*") {
            $resolved.Add("web")
        }
        elseif ($normalized -like "src/iM1os.Workers/*") {
            $resolved.Add("worker")
        }
        elseif ($normalized -like "src/iM1os.Api/*") {
            $resolved.Add("api")
        }
        elseif ($normalized -like "src/iM1os.Application/*" -or
                $normalized -like "src/iM1os.Domain/*" -or
                $normalized -like "src/iM1os.Infrastructure/*") {
            $resolved.Add("web")
            $resolved.Add("worker")
        }
    }

    if ($resolved.Count -eq 0) {
        $resolved.Add("web")
    }

    return @($resolved | Sort-Object -Unique)
}

function Get-DeployDevRisks {
    param([string[]]$ChangedFiles)

    $risks = New-Object System.Collections.Generic.List[string]
    foreach ($file in $ChangedFiles) {
        $normalized = $file.Replace("\", "/")
        if ($normalized -match "(^|/)Migrations/|ApplicationDbContext|IApplicationDbContext|DbContextModelSnapshot") {
            $risks.Add("Database or EF model changes detected: $file")
        }
        elseif ($normalized -match "\.csproj$|global\.json$|Directory\.Build\.props$|dotnet-tools\.json$") {
            $risks.Add("Dependency or build graph changes detected: $file")
        }
        elseif ($normalized -match "Program\.cs$|DependencyInjection\.cs$|appsettings|(^|/)\.env|Authentication|Authorization|Security|Configuration/") {
            $risks.Add("Startup, configuration, or security-sensitive changes detected: $file")
        }
        elseif ($normalized -match "^src/iM1os.Api/|^src/iM1os.Workers/") {
            $risks.Add("Non-web deployable changed and may need explicit service selection: $file")
        }
    }

    return @($risks | Sort-Object -Unique)
}

function New-RemoteDeployScript {
    param([string]$PackageName, [string[]]$ResolvedServices, [string]$RemoteHealthUrl)

    $serviceList = $ResolvedServices -join " "
    $script = @'
set -euo pipefail
NAME="__NAME__"
SERVICES="__SERVICES__"
PACKAGE="/tmp/im1os-dev-${NAME}.tgz"
RELEASE="/tmp/im1os-dev-${NAME}"
BACKUP_ROOT="/opt/im1os/backups/${NAME}"

service_dir() {
  case "$1" in
    web) echo "/opt/im1os/web" ;;
    worker) echo "/opt/im1os/worker" ;;
    api) echo "/opt/im1os/api" ;;
    *) echo "Unknown service: $1" >&2; exit 2 ;;
  esac
}

rm -rf "${RELEASE}"
mkdir -p "${RELEASE}" "${BACKUP_ROOT}"
tar -xzf "${PACKAGE}" -C "${RELEASE}"

for svc in ${SERVICES}; do
  sudo systemctl stop "im1os-${svc}" || true
done

for svc in ${SERVICES}; do
  target="$(service_dir "${svc}")"
  source="${RELEASE}/${svc}"
  if [ ! -d "${source}" ]; then
    echo "Package does not contain ${svc} output." >&2
    exit 3
  fi
  if [ -d "${target}" ]; then
    sudo cp -a "${target}" "${BACKUP_ROOT}/${svc}"
  fi
  sudo rm -rf "${target}"
  sudo mkdir -p "$(dirname "${target}")"
  sudo cp -a "${source}" "${target}"
  sudo chown -R im1admin:im1admin "${target}"
done

for svc in ${SERVICES}; do
  sudo systemctl start "im1os-${svc}"
  sleep 3
done

for svc in ${SERVICES}; do
  sudo systemctl is-active "im1os-${svc}"
done

if echo " ${SERVICES} " | grep -q " web "; then
  for attempt in $(seq 1 30); do
    if curl -fsS "__HEALTH_URL__"; then
      exit 0
    fi
    sleep 2
  done
  echo "Web health check did not pass after waiting." >&2
  exit 4
fi
'@

    return ($script.Replace("__NAME__", $PackageName).
        Replace("__SERVICES__", $serviceList).
        Replace("__HEALTH_URL__", $RemoteHealthUrl) -replace "`r`n", "`n")
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
Set-Location $repoRoot

if ([string]::IsNullOrWhiteSpace($KnownHostsFile)) {
    $KnownHostsFile = Join-Path $env:TEMP "im1os-dev-known-hosts.tmp"
}

$changedFiles = @(Get-ChangedFiles)
$resolvedServices = @(Resolve-Services -RequestedServices $Services -ChangedFiles $changedFiles)
$risks = @(Get-DeployDevRisks -ChangedFiles $changedFiles)

Write-Step "Deploy Dev preflight"
Write-Host ("Changed files: {0}" -f $changedFiles.Count)
Write-Host ("Services: {0}" -f ($resolvedServices -join ", "))

if ($risks.Count -gt 0) {
    Write-Warning "Deploy Dev may not be sufficient for these changes."
    foreach ($risk in $risks) {
        Write-Warning " - $risk"
    }
    Write-Warning "Use Deploy Platform for full commit/push/full-test deployment, or rerun Deploy Dev with -AcknowledgeRisk after explicit approval."
    if (-not $AcknowledgeRisk) {
        throw "Deploy Dev stopped because risky changes require acknowledgement."
    }
}

if (-not $SkipBuild) {
    Write-Step "Build solution"
    Invoke-External "dotnet" @("build", "iM1os.sln")
}

if (-not $SkipTests) {
    if (-not [string]::IsNullOrWhiteSpace($TestFilter)) {
        Write-Step "Run targeted tests"
        Invoke-External "dotnet" @("test", "tests\iM1os.Tests\iM1os.Tests.csproj", "--no-build", "--filter", $TestFilter)
    }
    elseif ($RunTests) {
        Write-Step "Run full test suite"
        Invoke-External "dotnet" @("test", "iM1os.sln", "--no-build")
    }
    else {
        Write-Warning "Deploy Dev did not run tests. Use -TestFilter for focused tests or -RunTests for the full suite."
    }
}

if ([string]::IsNullOrWhiteSpace($Name)) {
    $Name = "quick-" + (Get-Date -Format "yyyyMMddHHmmss")
}

$artifactRoot = Join-Path $repoRoot "artifacts\deploy-dev-$Name"
$packagePath = Join-Path $repoRoot "artifacts\im1os-dev-$Name.tgz"

Write-Step "Publish selected services"
New-Item -ItemType Directory -Force -Path $artifactRoot | Out-Null

$projectMap = @{
    web = "src\iM1os.Web\iM1os.Web.csproj"
    worker = "src\iM1os.Workers\iM1os.Workers.csproj"
    api = "src\iM1os.Api\iM1os.Api.csproj"
}

foreach ($service in $resolvedServices) {
    if (-not $projectMap.ContainsKey($service)) {
        throw "Unknown deploy service '$service'. Valid values: web, worker, api, auto."
    }
    $outputPath = Join-Path $artifactRoot $service
    New-Item -ItemType Directory -Force -Path $outputPath | Out-Null
    Invoke-External "dotnet" @("publish", $projectMap[$service], "-c", "Release", "-o", $outputPath, "--nologo")
}

Write-Step "Package publish output"
if (Test-Path $packagePath) {
    Remove-Item -LiteralPath $packagePath -Force
}
$tarArgs = @("-czf", $packagePath, "-C", $artifactRoot) + $resolvedServices
Invoke-External "tar" $tarArgs

Write-Step "Copy package to Dev"
Invoke-External "scp" @(
    "-o", "UserKnownHostsFile=$KnownHostsFile",
    "-o", "StrictHostKeyChecking=accept-new",
    $packagePath,
    "${DevHost}:/tmp/im1os-dev-$Name.tgz"
)

Write-Step "Restart Dev services"
$remoteScript = New-RemoteDeployScript -PackageName $Name -ResolvedServices $resolvedServices -RemoteHealthUrl $HealthUrl
Write-Host $remoteScript -ForegroundColor DarkGray
if (-not $DryRun) {
    $remoteScriptPath = Join-Path $env:TEMP "im1os-dev-$Name-deploy.sh"
    $remoteScriptRemotePath = "/tmp/im1os-dev-$Name-deploy.sh"
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($remoteScriptPath, $remoteScript + "`n", $utf8NoBom)

    Invoke-External "scp" @(
        "-o", "UserKnownHostsFile=$KnownHostsFile",
        "-o", "StrictHostKeyChecking=accept-new",
        $remoteScriptPath,
        "${DevHost}:$remoteScriptRemotePath"
    )

    ssh -o "UserKnownHostsFile=$KnownHostsFile" -o "StrictHostKeyChecking=accept-new" $DevHost "chmod +x '$remoteScriptRemotePath' && bash '$remoteScriptRemotePath'"
    if ($LASTEXITCODE -ne 0) {
        throw "Remote deployment failed with exit code $LASTEXITCODE."
    }
}

if ($resolvedServices -contains "web") {
    Write-Step "Verify public Dev health"
    Invoke-External "curl.exe" @("-I", "-fsS", $PublicHealthUrl)
}

Write-Step "Deploy Dev complete"
Write-Host "Package: $packagePath"
