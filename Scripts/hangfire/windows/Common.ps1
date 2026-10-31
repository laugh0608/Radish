Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-Condition([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}
function Write-Json([string]$Path, $Value) {
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($Path)) | Out-Null
    [IO.File]::WriteAllText($Path, (ConvertTo-Json -InputObject $Value -Depth 100) + "`n", [Text.UTF8Encoding]::new($false))
}
function Read-Json([string]$Path) { return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json -AsHashtable }
function Get-Digest([string]$Path, [string]$Algorithm = 'SHA256') { return (Get-FileHash -LiteralPath $Path -Algorithm $Algorithm).Hash.ToLowerInvariant() }
function Save-Owner { Write-Json (Join-Path $script:Root 'owner.json') $script:Owner }
function Assert-OwnedPath([string]$Path) {
    $full = [IO.Path]::GetFullPath($Path)
    Assert-Condition ($full.StartsWith($script:Root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) "Path escapes task root: $full"
    $cursor = $full
    while ($cursor.Length -ge $script:Root.Length) {
        if (Test-Path -LiteralPath $cursor) {
            Assert-Condition (-not ((Get-Item -LiteralPath $cursor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) "Reparse point refused: $cursor"
        }
        $cursor = [IO.Path]::GetDirectoryName($cursor)
        if ([string]::IsNullOrEmpty($cursor)) { break }
    }
    return $full
}
function Remove-Owned([string]$Path) {
    $full = Assert-OwnedPath $Path
    if (Test-Path -LiteralPath $full) {
        # Enumerate first: recursive deletion must never cross a child junction/symlink.
        Get-ChildItem -LiteralPath $full -Force -Recurse | ForEach-Object {
            Assert-Condition (-not ($_.Attributes -band [IO.FileAttributes]::ReparsePoint)) "Reparse point refused: $($_.FullName)"
        }
        Remove-Item -LiteralPath $full -Recurse -Force
    }
}
function Initialize-Task([string]$TaskRoot, [string]$ExpectedCommit, [bool]$Existing = $false) {
    Assert-Condition ($IsWindows -and [Environment]::Is64BitProcess -and $env:RUNNER_ARCH -eq 'X64' -and [Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture -eq [Runtime.InteropServices.Architecture]::X64 -and [Runtime.InteropServices.RuntimeInformation]::OSArchitecture -eq [Runtime.InteropServices.Architecture]::X64) 'Native Windows x64 runner required'
    Assert-Condition ($env:GITHUB_RUN_ID -match '^\d+$' -and $env:GITHUB_RUN_ATTEMPT -match '^\d+$') 'Runner identity missing'
    Assert-Condition ($ExpectedCommit -cmatch '^[a-f0-9]{40}$' -and $ExpectedCommit -ceq $env:GITHUB_SHA) 'Unapproved workflow commit'
    $id = "radish-hangfire-razor-$env:GITHUB_RUN_ID-$env:GITHUB_RUN_ATTEMPT"
    $expected = [IO.Path]::GetFullPath((Join-Path $env:RUNNER_TEMP $id))
    Assert-Condition ([IO.Path]::GetFullPath($TaskRoot) -ceq $expected) 'Task root differs from runner-owned location'
    Assert-Condition (-not ((Get-Item -LiteralPath $env:RUNNER_TEMP).Attributes -band [IO.FileAttributes]::ReparsePoint)) 'Runner temp is a reparse point'
    $script:Root = $expected
    if ($Existing) {
        $script:Owner = Read-Json (Join-Path $script:Root 'owner.json')
        Assert-Condition ($script:Owner.id -ceq $id -and $script:Owner.commit -ceq $ExpectedCommit) 'Foreign owner marker'
    } else {
        Assert-Condition (-not (Test-Path -LiteralPath $script:Root)) 'Task directory already exists'
        [IO.Directory]::CreateDirectory($script:Root) | Out-Null
        $script:Owner = @{ id = $id; commit = $ExpectedCommit; commands = @(); status = 'running'; createdUtc = [DateTime]::UtcNow.ToString('O'); cleanupComplete = $false }
        Save-Owner
        foreach ($name in @('work', 'evidence')) { [IO.Directory]::CreateDirectory((Join-Path $script:Root $name)) | Out-Null }
    }
    Assert-OwnedPath (Join-Path $script:Root 'owner.json') | Out-Null
    $script:Evidence = Join-Path $script:Root 'evidence'
    $script:Work = Join-Path $script:Root 'work'
    $script:Inputs = Read-Json (Join-Path $PSScriptRoot 'inputs.json')
}
function Get-ChildEnvironment([hashtable]$Extra = @{}) {
    # Keep Actions tokens, inherited build switches and business connection strings out of child logs/binlogs.
    $result = @{}
    foreach ($name in @('SystemRoot', 'SystemDrive', 'windir', 'ComSpec', 'ProgramFiles', 'ProgramFiles(x86)', 'ProgramW6432', 'USERPROFILE', 'NUMBER_OF_PROCESSORS', 'PROCESSOR_ARCHITECTURE', 'PATH', 'PATHEXT')) {
        $value = [Environment]::GetEnvironmentVariable($name)
        if ($null -ne $value) { $result[$name] = $value }
    }
    $temp = Join-Path $script:Work 'tmp'
    [IO.Directory]::CreateDirectory($temp) | Out-Null
    foreach ($name in @('TEMP', 'TMP')) { $result[$name] = $temp }
    $result.DOTNET_CLI_HOME = Join-Path $script:Work 'cli-home'
    $result.NUGET_HTTP_CACHE_PATH = Join-Path $script:Work 'http-cache'
    $result.NUGET_PACKAGES = Join-Path $script:Work 'packages'
    $result.DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $result.DOTNET_CLI_UI_LANGUAGE = 'en'
    $result.DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
    $result.DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
    $result.DOTNET_CLI_USE_MSBUILD_SERVER = '0'
    $result.MSBUILDDISABLENODEREUSE = '1'
    $result.GIT_CONFIG_GLOBAL = 'NUL'
    $result.GIT_CONFIG_NOSYSTEM = '1'
    $result.GIT_TERMINAL_PROMPT = '0'
    if ($script:Owner.ContainsKey('dotnet')) {
        $result.DOTNET_ROOT = [IO.Path]::GetDirectoryName($script:Owner.dotnet)
        $result.DOTNET_ROOT_X64 = $result.DOTNET_ROOT
        $result.PATH = $result.DOTNET_ROOT + ';' + $result.PATH
    }
    foreach ($name in $Extra.Keys) { $result[$name] = $Extra[$name] }
    return $result
}
function Stop-OwnedProcess($Record) {
    $process = Get-Process -Id $Record.pid -ErrorAction SilentlyContinue
    if ($null -eq $process) { return }
    Assert-Condition ($process.StartTime.ToUniversalTime().ToString('O') -ceq $Record.creationTime) 'PID reused; refusing termination'
    Assert-Condition ([IO.Path]::GetFullPath($process.Path) -ieq $Record.executable) 'Process executable differs; refusing termination'
    $process.Kill($true)
    Assert-Condition ($process.WaitForExit(10000)) 'Owned process did not exit'
}
function Invoke-OwnedCommand([string]$Executable, [string[]]$Arguments, [string]$Name, [string]$Directory = $script:Work, [int]$TimeoutSeconds = 600, [hashtable]$Environment = @{}) {
    $resolved = (Get-Command $Executable -CommandType Application -ErrorAction Stop).Source
    $log = Join-Path $script:Evidence ("commands/{0:D4}-{1}.log" -f ($script:Owner.commands.Count + 1), $Name)
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($log)) | Out-Null
    $info = [Diagnostics.ProcessStartInfo]::new($resolved)
    $info.WorkingDirectory = $Directory
    $info.UseShellExecute = $false
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    foreach ($argument in $Arguments) { $info.ArgumentList.Add($argument) }
    $info.Environment.Clear()
    $childEnv = Get-ChildEnvironment $Environment
    foreach ($name in $childEnv.Keys) { $info.Environment[$name] = $childEnv[$name] }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $info
    Assert-Condition ($process.Start()) 'Process failed to start'
    $record = @{ name = $Name; executable = $resolved; arguments = $Arguments; cwd = $Directory; pid = $process.Id; creationTime = $process.StartTime.ToUniversalTime().ToString('O'); log = [IO.Path]::GetRelativePath($script:Evidence, $log); completed = $false }
    $script:Owner.commands += $record
    Save-Owner
    $stdout = $process.StandardOutput.ReadToEndAsync()
    $stderr = $process.StandardError.ReadToEndAsync()
    try {
        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) { Stop-OwnedProcess $record;throw "Command timed out: $Name" }
        $text = $stdout.GetAwaiter().GetResult() + $stderr.GetAwaiter().GetResult()
        [IO.File]::WriteAllText($log, $text, [Text.UTF8Encoding]::new($false))
        $record.exitCode = $process.ExitCode
        $record.completed = $true
        Assert-Condition ($process.ExitCode -eq 0) "Command failed: $Name; see $log"
        return $text
    } finally {
        if (-not $record.completed) {
            Stop-OwnedProcess $record
            [IO.File]::WriteAllText($log, $stdout.GetAwaiter().GetResult() + $stderr.GetAwaiter().GetResult(), [Text.UTF8Encoding]::new($false))
        }
        $record.end = [DateTime]::UtcNow.ToString('O')
        Save-Owner
        $process.Dispose()
    }
}
