<#
.SYNOPSIS
Full platform deployment path: verify, commit, push, then deploy Dev.

.DESCRIPTION
Deploy Platform is for changes that are ready to become the shared platform
revision. It runs the full build and test suite, commits staged changes, pushes
the branch, and then calls deploy-dev.ps1 from the committed revision.

By default this script expects files to already be staged so the commit scope is
explicit. Use -StageAll only when the entire working tree is intentionally part
of the platform deployment.

.EXAMPLE
git add src docs tests
.\deploy\deploy-platform.ps1 -CommitMessage "Add payments MVP workflow"

.EXAMPLE
.\deploy\deploy-platform.ps1 -CommitMessage "Deploy all current platform changes" -StageAll
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$CommitMessage,
    [string[]]$Services = @("auto"),
    [string]$DevHost = "im1admin@im1-dev.im1os.com",
    [switch]$StageAll,
    [switch]$SkipDeploy,
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

function Resolve-ServicesFromFiles {
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

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
Set-Location $repoRoot

Write-Step "Deploy Platform preflight"
if ($StageAll) {
    Invoke-External "git" @("add", "-A")
}

$stagedFiles = @(git diff --cached --name-only)
if ($stagedFiles.Count -eq 0) {
    throw "No staged files found. Stage the intended platform changes first, or rerun with -StageAll."
}

$resolvedServices = @(Resolve-ServicesFromFiles -RequestedServices $Services -ChangedFiles $stagedFiles)
Write-Host ("Staged files: {0}" -f $stagedFiles.Count)
Write-Host ("Services: {0}" -f ($resolvedServices -join ", "))

Write-Step "Build solution"
Invoke-External "dotnet" @("build", "iM1os.sln")

Write-Step "Run full test suite"
Invoke-External "dotnet" @("test", "iM1os.sln", "--no-build")

Write-Step "Commit staged changes"
Invoke-External "git" @("commit", "-m", $CommitMessage)

$commitSha = if ($DryRun) { "dry-run" } else { (git rev-parse --short HEAD).Trim() }

Write-Step "Push commit"
Invoke-External "git" @("push")

if ($SkipDeploy) {
    Write-Step "Deploy Platform complete without Dev deployment"
    Write-Host "Commit: $commitSha"
    return
}

Write-Step "Deploy committed revision to Dev"
$deployDev = Join-Path $PSScriptRoot "deploy-dev.ps1"
$deployName = "platform-$commitSha-" + (Get-Date -Format "yyyyMMddHHmmss")
$deployArgs = @(
    "-Services"
) + $resolvedServices + @(
    "-Name", $deployName,
    "-DevHost", $DevHost,
    "-SkipBuild",
    "-SkipTests",
    "-AcknowledgeRisk"
)

if ($DryRun) {
    $deployArgs += "-DryRun"
}

& $deployDev @deployArgs
if ($LASTEXITCODE -ne 0) {
    throw "Deploy Dev failed after platform commit."
}

Write-Step "Deploy Platform complete"
Write-Host "Commit: $commitSha"
