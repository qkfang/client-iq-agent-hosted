#Requires -Version 7.0
<#
.SYNOPSIS
    Register a confidential web app in Microsoft Entra so the Onboarding CRM web
    app can sign users in and call the Foundry Responses API on their behalf.

.DESCRIPTION
    Automates the Entra app registration the web app needs for On-Behalf-Of:
      1. Create a confidential web app registration.
      2. Create its service principal.
      3. Configure the web redirect URIs (/signin-oidc) for local dev and,
         optionally, the deployed App Service URL.
      4. Create a client secret.
      5. Add the delegated Azure AI permission (used to acquire a user token for
         https://ai.azure.com/.default) and grant admin consent.
      6. Optionally write ClientId/TenantId into appsettings.json and store the
         client secret in dotnet user-secrets (kept out of source control).

    Requires the Azure CLI (az) signed in with `az login`, and permission to
    create app registrations and grant admin consent.

.PARAMETER DisplayName
    Display name for the new app registration.

.PARAMETER AppServiceUrl
    Optional deployed web app base URL, e.g. https://app-xxxx.azurewebsites.net.
    Its /signin-oidc path is added as a redirect URI.

.PARAMETER LocalUrls
    Local dev base URLs whose /signin-oidc path is added as redirect URIs.

.PARAMETER UpdateAppSettings
    Write the resolved ClientId and TenantId into ../appsettings.json.

.PARAMETER SetUserSecret
    Store the generated client secret in dotnet user-secrets for the web project.

.EXAMPLE
    ./Register-WebApp.ps1 -UpdateAppSettings -SetUserSecret

.EXAMPLE
    ./Register-WebApp.ps1 -AppServiceUrl https://app-ciquocsj.azurewebsites.net
#>
[CmdletBinding()]
param(
    [string]$DisplayName = 'IQ Onboarding Web',
    [string]$AppServiceUrl,
    [string[]]$LocalUrls = @('https://localhost:5001', 'http://localhost:5000'),
    [switch]$UpdateAppSettings,
    [switch]$SetUserSecret
)

$ErrorActionPreference = 'Stop'

# Resource behind the Foundry Responses API. The web app acquires a delegated
# user token for this resource (scope https://ai.azure.com/.default), which lets
# tools such as Work IQ run On-Behalf-Of the signed-in user.
$AiResource = 'https://ai.azure.com'

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    throw 'Azure CLI (az) is not installed or not on PATH.'
}

$TenantId = az account show --query tenantId -o tsv
if (-not $TenantId) {
    throw 'Not signed in. Run "az login" first.'
}
Write-Host "Signed-in tenant: $TenantId" -ForegroundColor Cyan

# Build the web redirect URIs.
$redirectUris = @()
foreach ($u in $LocalUrls) { $redirectUris += "$($u.TrimEnd('/'))/signin-oidc" }
if ($AppServiceUrl) { $redirectUris += "$($AppServiceUrl.TrimEnd('/'))/signin-oidc" }

Write-Host "Creating app registration '$DisplayName'..." -ForegroundColor Yellow
$AppId = az ad app create `
    --display-name $DisplayName `
    --sign-in-audience AzureADMyOrg `
    --web-redirect-uris $redirectUris `
    --query appId -o tsv
Write-Host "APP_ID: $AppId" -ForegroundColor Cyan

Write-Host 'Creating service principal...' -ForegroundColor Yellow
az ad sp create --id $AppId | Out-Null

Write-Host 'Creating client secret...' -ForegroundColor Yellow
$ClientSecret = az ad app credential reset --id $AppId --display-name 'web-signin' --years 1 --query password -o tsv

# Add the delegated Azure AI permission so On-Behalf-Of token acquisition works.
Write-Host 'Resolving Azure AI delegated permission...' -ForegroundColor Yellow
$aiSp = az ad sp list --filter "servicePrincipalNames/any(n:n eq '$AiResource')" --query "[0]" -o json | ConvertFrom-Json
if ($aiSp) {
    $scope = $aiSp.oauth2PermissionScopes | Where-Object { $_.value -eq 'user_impersonation' } | Select-Object -First 1
    if ($scope) {
        az ad app permission add --id $AppId --api $aiSp.appId --api-permissions "$($scope.id)=Scope" | Out-Null
        Write-Host 'Granting admin consent...' -ForegroundColor Yellow
        az ad app permission admin-consent --id $AppId | Out-Null
    }
    else {
        Write-Warning "user_impersonation scope not found on $AiResource. Add the delegated Azure AI permission and grant admin consent manually."
    }
}
else {
    Write-Warning "Service principal for $AiResource not found in this tenant. Grant the web app delegated Azure AI access and admin consent manually."
}

$webRoot = Split-Path -Parent $PSScriptRoot
$appsettings = Join-Path $webRoot 'appsettings.json'

if ($UpdateAppSettings) {
    Write-Host 'Updating appsettings.json...' -ForegroundColor Yellow
    $json = Get-Content $appsettings -Raw | ConvertFrom-Json
    $json.AzureAd.TenantId = $TenantId
    $json.AzureAd.ClientId = $AppId
    $json | ConvertTo-Json -Depth 20 | Set-Content $appsettings
}

if ($SetUserSecret) {
    Write-Host 'Storing client secret in dotnet user-secrets...' -ForegroundColor Yellow
    Push-Location $webRoot
    dotnet user-secrets set 'AzureAd:ClientSecret' $ClientSecret | Out-Null
    Pop-Location
}

Write-Host ''
Write-Host 'Done. App registration values:' -ForegroundColor Green
Write-Host "  TENANT_ID     : $TenantId"
Write-Host "  CLIENT_ID     : $AppId"
Write-Host "  REDIRECT_URIS : $($redirectUris -join ', ')"
if (-not $SetUserSecret) {
    Write-Host "  CLIENT_SECRET : $ClientSecret"
    Write-Host ''
    Write-Host 'Store the secret securely (dotnet user-secrets or Key Vault); do not commit it.' -ForegroundColor Yellow
}
