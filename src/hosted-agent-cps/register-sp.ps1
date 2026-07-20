<#
.SYNOPSIS
    Registers the Entra app / service principal used by the hosted Copilot Studio agent
    and grants it the Power Platform "CopilotStudio.Copilots.Invoke" application permission.

.DESCRIPTION
    Creates (or reuses) an app registration named "sp-demo-cps-agent", ensures a matching
    service principal exists, adds the Power Platform API application permission required to
    call the Copilot Studio Direct-to-Engine API app-only, grants admin consent, and issues a
    client secret. The resulting values map directly to the fields in .env.

.NOTES
    Requires the Azure CLI (az) and an account with permission to create app registrations
    and grant admin consent (Application Administrator / Cloud Application Administrator or
    Global Administrator). Run: az login
#>

[CmdletBinding()]
param(
    [string]$DisplayName = "sp-demo-cps-agent",
    [switch]$WriteEnv
)

$ErrorActionPreference = "Stop"

# Power Platform API (first-party app) and the application permission required for app-only invoke.
$PowerPlatformApiAppId = "8578e004-a5c6-46e7-913e-12f58912df43"
$PermissionValue = "CopilotStudio.Copilots.Invoke"

Write-Host "Checking Azure CLI login..."
$account = az account show 2>$null | ConvertFrom-Json
if (-not $account) {
    throw "Not logged in. Run 'az login' first."
}
$tenantId = $account.tenantId
Write-Host "Tenant: $tenantId"

Write-Host "Ensuring Power Platform API service principal is provisioned..."
$ppSp = az ad sp show --id $PowerPlatformApiAppId 2>$null | ConvertFrom-Json
if (-not $ppSp) {
    az ad sp create --id $PowerPlatformApiAppId | Out-Null
    $ppSp = az ad sp show --id $PowerPlatformApiAppId | ConvertFrom-Json
}

$roleId = ($ppSp.appRoles | Where-Object { $_.value -eq $PermissionValue } | Select-Object -First 1).id
if (-not $roleId) {
    throw "Could not resolve app role '$PermissionValue' on the Power Platform API."
}
Write-Host "Resolved permission '$PermissionValue' -> $roleId"

Write-Host "Creating or reusing app registration '$DisplayName'..."
$appId = az ad app list --display-name $DisplayName --query "[0].appId" -o tsv
if (-not $appId) {
    $appId = az ad app create --display-name $DisplayName --sign-in-audience AzureADMyOrg --query appId -o tsv
}
Write-Host "App (client) ID: $appId"

Write-Host "Ensuring service principal exists..."
$sp = az ad sp show --id $appId 2>$null | ConvertFrom-Json
if (-not $sp) {
    az ad sp create --id $appId | Out-Null
}

Write-Host "Adding application permission..."
az ad app permission add --id $appId --api $PowerPlatformApiAppId --api-permissions "$roleId=Role" | Out-Null

Write-Host "Granting admin consent (may take a few seconds to propagate)..."
az ad app permission admin-consent --id $appId | Out-Null

Write-Host "Creating client secret..."
$clientSecret = az ad app credential reset --id $appId --display-name "cps-agent" --years 1 --query password -o tsv

Write-Host ""
Write-Host "==================== Copy these into .env ===================="
Write-Host "AZURE_TENANT_ID=$tenantId"
Write-Host "AZURE_CLIENT_ID=$appId"
Write-Host "AZURE_CLIENT_SECRET=$clientSecret"
Write-Host "============================================================="
Write-Host "Still required (from Copilot Studio): ENVIRONMENT_ID, AGENT_IDENTIFIER"

if ($WriteEnv) {
    $envPath = Join-Path $PSScriptRoot ".env"
    if (Test-Path $envPath) {
        $content = Get-Content $envPath -Raw
        $content = $content -replace "AZURE_TENANT_ID=.*", "AZURE_TENANT_ID=$tenantId"
        $content = $content -replace "AZURE_CLIENT_ID=.*", "AZURE_CLIENT_ID=$appId"
        $content = $content -replace "AZURE_CLIENT_SECRET=.*", "AZURE_CLIENT_SECRET=$clientSecret"
        Set-Content -Path $envPath -Value $content -NoNewline
        Write-Host "Updated $envPath (ENVIRONMENT_ID and AGENT_IDENTIFIER still need manual values)."
    }
    else {
        Write-Host "No .env found at $envPath; skipped writing."
    }
}
