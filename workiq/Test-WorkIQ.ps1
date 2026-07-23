#Requires -Version 7.0
<#
.SYNOPSIS
    Test a tenant's Work IQ configuration using the A2A v1.0 protocol.

.DESCRIPTION
    Acquires a delegated user token for Work IQ (audience api://workiq.svc.cloud.microsoft,
    scope WorkIQAgent.Ask) via the OAuth 2.0 device code flow, then sends messages to the
    Work IQ Gateway using the A2A v1.0 JSON-RPC SendMessage method. Multi-turn context is
    preserved via the returned contextId.

    Follows: https://learn.microsoft.com/microsoft-365/copilot/extensibility/work-iq/a2a/quickstart

.PARAMETER AppId
    The Application (client) ID of the public-client app registered in Microsoft Entra.

.PARAMETER TenantId
    The Directory (tenant) ID.

.PARAMETER AgentId
    Optional Work IQ agent ID to target a specific agent. Treated as an opaque string.

.PARAMETER Message
    Optional single message to send. When omitted, an interactive prompt loop starts.

.EXAMPLE
    ./Test-WorkIQ.ps1 -AppId <APP_ID> -TenantId <TENANT_ID>

.EXAMPLE
    ./Test-WorkIQ.ps1 -AppId <APP_ID> -TenantId <TENANT_ID> -Message "What meetings do I have today?"
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$AppId,

    [Parameter(Mandatory)]
    [string]$TenantId,

    [string]$AgentId,

    [string]$Message
)

$ErrorActionPreference = 'Stop'

$Gateway = 'https://workiq.svc.cloud.microsoft/a2a/'
$Scope = 'api://workiq.svc.cloud.microsoft/WorkIQAgent.Ask'
$Authority = "https://login.microsoftonline.com/$TenantId/oauth2/v2.0"

function Get-WorkIQToken {
    param([string]$ClientId, [string]$TokenScope, [string]$AuthorityBase)

    $device = Invoke-RestMethod -Method Post -Uri "$AuthorityBase/devicecode" -Body @{
        client_id = $ClientId
        scope     = "$TokenScope offline_access"
    }

    Write-Host ''
    Write-Host $device.message -ForegroundColor Cyan
    Write-Host ''

    $interval = [int]$device.interval
    $deadline = (Get-Date).AddSeconds([int]$device.expires_in)

    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Seconds $interval
        try {
            $token = Invoke-RestMethod -Method Post -Uri "$AuthorityBase/token" -Body @{
                grant_type  = 'urn:ietf:params:oauth:grant-type:device_code'
                client_id   = $ClientId
                device_code = $device.device_code
            }
            return $token.access_token
        }
        catch {
            $err = $_.ErrorDetails.Message | ConvertFrom-Json -ErrorAction SilentlyContinue
            switch ($err.error) {
                'authorization_pending' { continue }
                'slow_down' { $interval += 5; continue }
                default { throw "Token error: $($err.error) - $($err.error_description)" }
            }
        }
    }
    throw 'Device code expired before sign-in completed.'
}

function Send-WorkIQMessage {
    param(
        [string]$Text,
        [string]$Token,
        [string]$ContextId,
        [string]$Agent
    )

    $offsetMinutes = [int][System.TimeZoneInfo]::Local.GetUtcOffset((Get-Date)).TotalMinutes

    $message = [ordered]@{
        role      = 'ROLE_USER'
        messageId = [guid]::NewGuid().ToString()
        parts     = @(@{ text = $Text })
        metadata  = @{
            Location = @{
                timeZoneOffset = $offsetMinutes
                timeZone       = [System.TimeZoneInfo]::Local.Id
            }
        }
    }
    if ($ContextId) { $message.contextId = $ContextId }
    if ($Agent) { $message.metadata.AgentId = $Agent }

    $body = @{
        jsonrpc = '2.0'
        id      = [guid]::NewGuid().ToString()
        method  = 'SendMessage'
        params  = @{ message = $message }
    } | ConvertTo-Json -Depth 10

    $headers = @{
        Authorization  = "Bearer $Token"
        'Content-Type' = 'application/json'
        'A2A-Version'  = '1.0'
    }

    return Invoke-RestMethod -Method Post -Uri $Gateway -Headers $headers -Body $body
}

function Write-AgentReply {
    param($Response)

    $task = $Response.result.task
    $state = $task.status.state
    $text = ($task.artifacts.parts | Where-Object { $_.text } | ForEach-Object { $_.text }) -join "`n"

    if (-not $text) { $text = '(no text returned)' }
    Write-Host "Agent > $text" -ForegroundColor Green
    Write-Verbose "State: $state  ContextId: $($task.contextId)"
    return $task.contextId
}

Write-Host 'Acquiring Work IQ token (device code sign-in required)...' -ForegroundColor Yellow
$accessToken = Get-WorkIQToken -ClientId $AppId -TokenScope $Scope -AuthorityBase $Authority

Write-Host ''
Write-Host "-- READY -- Work IQ Gateway -- $Gateway --" -ForegroundColor Yellow

$context = $null

if ($Message) {
    $response = Send-WorkIQMessage -Text $Message -Token $accessToken -ContextId $context -Agent $AgentId
    Write-AgentReply -Response $response | Out-Null
    return
}

Write-Host "Type a message. 'quit' to exit."
while ($true) {
    $input = Read-Host 'You'
    if ([string]::IsNullOrWhiteSpace($input)) { continue }
    if ($input -eq 'quit') { break }

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $response = Send-WorkIQMessage -Text $input -Token $accessToken -ContextId $context -Agent $AgentId
    $sw.Stop()

    $context = Write-AgentReply -Response $response
    Write-Host "  ($($sw.ElapsedMilliseconds) ms)" -ForegroundColor DarkGray
}
