#Requires -Version 7.0
<#
.SYNOPSIS
    Register a public-client app for Work IQ using the Azure CLI in the currently signed-in tenant.

.DESCRIPTION
    Automates the Azure CLI steps from the Work IQ A2A quickstart:
      1. Create a public-client app registration.
      2. Create its service principal.
      3. Configure the three public-client redirect URIs.
      4. Add the WorkIQAgent.Ask delegated permission and grant admin consent.
      5. Report the APP_ID and TENANT_ID of the signed-in tenant.

    Requires the Azure CLI (az) and an account signed in with `az login`.
    Follows: https://learn.microsoft.com/microsoft-365/copilot/extensibility/work-iq/a2a/quickstart?tabs=azure-cli

.PARAMETER DisplayName
    Display name for the new app registration.

.EXAMPLE
    ./Register-WorkIQApp.ps1
#>
[CmdletBinding()]
param(
    [string]$DisplayName = 'Work IQ Samples Client'
)

$ErrorActionPreference = 'Stop'

# Work IQ resource app ID and the WorkIQAgent.Ask delegated scope ID (constants from the quickstart).
$WorkIQAppId = 'fdcc1f02-fc51-4226-8753-f668596af7f7'
$WorkIQAskScopeId = '0b1715fd-f4bf-4c63-b16d-5be31f9847c2'

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    throw 'Azure CLI (az) is not installed or not on PATH.'
}

$TenantId = az account show --query tenantId -o tsv
if (-not $TenantId) {
    throw 'Not signed in. Run "az login" first.'
}
Write-Host "Signed-in tenant: $TenantId" -ForegroundColor Cyan

Write-Host "Creating app registration '$DisplayName'..." -ForegroundColor Yellow
$AppId = az ad app create `
    --display-name $DisplayName `
    --sign-in-audience AzureADMyOrg `
    --is-fallback-public-client true `
    --query appId -o tsv
Write-Host "APP_ID: $AppId" -ForegroundColor Cyan

Write-Host 'Creating service principal...' -ForegroundColor Yellow
az ad sp create --id $AppId | Out-Null

Write-Host 'Configuring public-client redirect URIs...' -ForegroundColor Yellow
az ad app update --id $AppId `
    --public-client-redirect-uris `
    'http://localhost' `
    'https://login.microsoftonline.com/common/oauth2/nativeclient' `
    "ms-appx-web://microsoft.aad.brokerplugin/$AppId" | Out-Null

Write-Host 'Adding WorkIQAgent.Ask delegated permission...' -ForegroundColor Yellow
az ad app permission add --id $AppId `
    --api $WorkIQAppId `
    --api-permissions "$WorkIQAskScopeId=Scope" | Out-Null

Write-Host 'Granting admin consent...' -ForegroundColor Yellow
az ad app permission admin-consent --id $AppId | Out-Null

Write-Host ''
Write-Host 'Done. Use these values with Test-WorkIQ.ps1:' -ForegroundColor Green
Write-Host "  APP_ID    : $AppId"
Write-Host "  TENANT_ID : $TenantId"
Write-Host ''
Write-Host "  ./Test-WorkIQ.ps1 -AppId $AppId -TenantId $TenantId"
