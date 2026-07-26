#Requires -Version 7.0
<#
.SYNOPSIS
    Load infra/.env into the current session and the azd environment.

.DESCRIPTION
    Reads the git-ignored infra/.env file and, for every KEY=VALUE entry, sets
    the value both as a process environment variable (so tools launched from this
    session see it) and in the active azd environment via `azd env set` (so Bicep
    parameter substitution and post-provision hooks pick it up).

    Wired as an azd `preprovision` hook so `azd up` / `azd provision` load these
    variables automatically. Missing file or missing azd are non-fatal.
#>
[CmdletBinding()]
param(
    [string]$EnvFile
)

$ErrorActionPreference = 'Stop'

if (-not $EnvFile) {
    $infraRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
    $EnvFile = Join-Path $infraRoot '.env'
}

if (-not (Test-Path $EnvFile)) {
    Write-Host "infra/.env not found at '$EnvFile' - skipping env load." -ForegroundColor Yellow
    return
}

$azdAvailable = [bool](Get-Command azd -ErrorAction SilentlyContinue)
if (-not $azdAvailable) {
    Write-Host 'azd not found on PATH - setting process variables only.' -ForegroundColor Yellow
}

Write-Host "Loading environment variables from '$EnvFile'..." -ForegroundColor Cyan

foreach ($line in Get-Content $EnvFile) {
    $trimmed = $line.Trim()
    if (-not $trimmed -or $trimmed.StartsWith('#')) { continue }

    $separator = $trimmed.IndexOf('=')
    if ($separator -lt 1) { continue }

    $key = $trimmed.Substring(0, $separator).Trim()
    $value = $trimmed.Substring($separator + 1).Trim()

    # Do not overwrite values already provided by the surrounding environment.
    $existing = [Environment]::GetEnvironmentVariable($key)
    if ([string]::IsNullOrEmpty($existing)) {
        Set-Item -Path "env:$key" -Value $value
    }

    if ($azdAvailable -and $value) {
        azd env set $key $value | Out-Null
    }

    Write-Host "  $key" -ForegroundColor DarkGray
}

Write-Host 'Environment variables loaded.' -ForegroundColor Green
