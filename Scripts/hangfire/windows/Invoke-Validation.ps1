[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('Inventory', 'Verify', 'Cleanup')][string]$Phase,
    [Parameter(Mandatory)][string]$TaskRoot,
    [Parameter(Mandatory)][string]$ExpectedCommit,
    [string]$ExpectedImageVersion = '',
    [string]$ExpectedVsVersion = '',
    [string]$ExpectedMsbuildVersion = '',
    [string]$ExpectedMsbuildSha256 = ''
)
. "$PSScriptRoot/Common.ps1"
. "$PSScriptRoot/Inventory.ps1"
. "$PSScriptRoot/Contracts.ps1"
. "$PSScriptRoot/Acquire.ps1"
. "$PSScriptRoot/Sources.ps1"
. "$PSScriptRoot/Verification.ps1"
. "$PSScriptRoot/Cleanup.ps1"

if ($Phase -eq 'Cleanup') {
    Initialize-Task $TaskRoot $ExpectedCommit $true
    if ($script:Owner.cleanupComplete) { Publish-ReviewedEvidence } else { Complete-OwnedCleanup }
    exit 0
}
Initialize-Task $TaskRoot $ExpectedCommit
$script:Owner.mode = $Phase
Save-Owner
try {
    $progress = New-WindowsInventory
    Invoke-PowerShellContracts
    $inventory = Get-WindowsInventory $progress
    if ($Phase -eq 'Verify') {
        Assert-ReviewedInventory $inventory $ExpectedImageVersion $ExpectedVsVersion $ExpectedMsbuildVersion $ExpectedMsbuildSha256
        Invoke-WindowsVerification $inventory
    }
    $script:Owner.status = 'passed'
} catch {
    $script:Owner.status = 'failed'
    Write-Json (Join-Path $script:Evidence 'failure.json') @{ message = $_.Exception.Message; detail = $_.ToString(); phase = $Phase }
    throw
} finally {
    Save-Owner
    Complete-OwnedCleanup
}
