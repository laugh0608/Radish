param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$DotNetArgs
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$dotnetHome = Join-Path $repoRoot '.dotnet-home'
$appData = Join-Path $dotnetHome 'AppData\Roaming'
$localAppData = Join-Path $dotnetHome 'AppData\Local'
$nugetConfigSource = Join-Path $repoRoot 'NuGet.Config'
$nugetConfigDirectory = Join-Path $appData 'NuGet'
$nugetConfigTarget = Join-Path $nugetConfigDirectory 'NuGet.Config'

New-Item -ItemType Directory -Force -Path $dotnetHome | Out-Null
New-Item -ItemType Directory -Force -Path $nugetConfigDirectory | Out-Null
New-Item -ItemType Directory -Force -Path $localAppData | Out-Null

if (Test-Path $nugetConfigSource) {
    Copy-Item $nugetConfigSource $nugetConfigTarget -Force
}

$env:DOTNET_CLI_HOME = $dotnetHome
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:APPDATA = $appData
$env:LOCALAPPDATA = $localAppData

if (-not $DotNetArgs -or $DotNetArgs.Count -eq 0) {
    Write-Host '用法: ./Scripts/dotnet-local.ps1 <dotnet 子命令>'
    exit 1
}

$forwardArgs = @($DotNetArgs)
$primaryCommand = $DotNetArgs[0].ToLowerInvariant()
$supportsAuditToggle = @('restore', 'build', 'test') -contains $primaryCommand
$hasAuditOverride = $DotNetArgs | Where-Object {
    $_ -like '-p:NuGetAudit=*' -or
    $_ -like '/p:NuGetAudit=*' -or
    $_ -like '--property:NuGetAudit=*'
}

if ($supportsAuditToggle -and -not $hasAuditOverride) {
    $forwardArgs += '-p:NuGetAudit=false'
}

$hasNoWarnOverride = $DotNetArgs | Where-Object {
    $_ -like '-p:NoWarn=*' -or
    $_ -like '/p:NoWarn=*' -or
    $_ -like '--property:NoWarn=*'
}

if ($supportsAuditToggle -and -not $hasNoWarnOverride) {
    $forwardArgs += '-p:NoWarn=NU1903'
}

$hasMaxCpuCountOverride = $DotNetArgs | Where-Object {
    $_ -match '^(--maxcpucount|-m(?::.*)?|/m(?::.*)?)$'
}

if (($primaryCommand -eq 'build' -or $primaryCommand -eq 'test') -and -not $hasMaxCpuCountOverride) {
    $forwardArgs += '-m:1'
}

$hasRestoreParallelOverride = $DotNetArgs | Where-Object { $_ -eq '--disable-parallel' }

if ($primaryCommand -eq 'restore' -and -not $hasRestoreParallelOverride) {
    $forwardArgs += '--disable-parallel'
}

& dotnet @forwardArgs
exit $LASTEXITCODE
