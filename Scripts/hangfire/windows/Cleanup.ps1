function Complete-OwnedCleanup {
    if ($script:Owner.cleanupComplete) { return }
    $errors = [Collections.Generic.List[string]]::new()
    foreach ($command in $script:Owner.commands) {
        if (-not $command.completed) { try { Stop-OwnedProcess $command } catch { $errors.Add($_.Exception.Message) } }
    }
    try {
        $candidates = @(Get-CimInstance Win32_Process | Where-Object {
            $_.ProcessId -ne $PID -and $_.Name -match '^(dotnet|MSBuild|MSBuildTaskHost|csc|VBCSCompiler)\.exe$' -and
            $null -ne $_.CommandLine -and $_.CommandLine.Contains($script:Root, [StringComparison]::OrdinalIgnoreCase)
        })
        foreach ($candidate in $candidates) {
            Assert-Condition ($candidate.CreationDate.ToUniversalTime() -ge [DateTime]::Parse($script:Owner.createdUtc).ToUniversalTime()) 'Candidate predates task; refusing termination'
            $process = Get-Process -Id $candidate.ProcessId -ErrorAction Stop
            Stop-OwnedProcess @{ pid = $process.Id; creationTime = $process.StartTime.ToUniversalTime().ToString('O'); executable = $process.Path }
        }
    } catch { $errors.Add($_.Exception.Message) }
    if ($errors.Count -eq 0) { try { Remove-Owned $script:Work } catch { $errors.Add($_.Exception.Message) } }
    $script:Owner.cleanupComplete = $errors.Count -eq 0
    if ($errors.Count -gt 0) { $script:Owner.status = 'failed' }
    Write-Json (Join-Path $script:Evidence 'commands.json') $script:Owner.commands
    Write-Json (Join-Path $script:Evidence 'cleanup.json') @{ complete = $script:Owner.cleanupComplete; workingTreeRemoved = -not (Test-Path -LiteralPath $script:Work); errors = @($errors); ports = @(); database = $null; globalToolsChanged = $false }
    Write-Json (Join-Path $script:Evidence 'result.json') @{ mode = $script:Owner.mode; status = $script:Owner.status; commit = $script:Owner.commit; windowsValidationComplete = $script:Owner.mode -eq 'Verify' -and $script:Owner.status -eq 'passed' -and $script:Owner.cleanupComplete }
    Save-Owner
    Publish-ReviewedEvidence
    Assert-Condition ($errors.Count -eq 0) 'Owned cleanup failed; inspect retained evidence'
}
function Publish-ReviewedEvidence {
    $destination = Join-Path $script:Root 'publish'
    if (Test-Path -LiteralPath $destination) { Remove-Owned $destination }
    [IO.Directory]::CreateDirectory($destination) | Out-Null
    $manifest = @()
    foreach ($file in Get-ChildItem -LiteralPath $script:Evidence -File -Recurse -Force) {
        Assert-OwnedPath $file.FullName | Out-Null
        $relative = [IO.Path]::GetRelativePath($script:Evidence, $file.FullName)
        $allowed = $file.Extension -cin @('.json', '.log', '.xml', '.cs', '.patch', '.binlog') -or $relative -cin @('licenses\COPYING', 'licenses\COPYING.LESSER', 'licenses\Cronos-LICENSE.txt')
        Assert-Condition ($allowed) "Unexpected evidence file blocked from upload: $relative"
        $target = Join-Path $destination $relative
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target)) | Out-Null
        Copy-Item -LiteralPath $file.FullName -Destination $target
        $manifest += @{ path = $relative.Replace('\', '/'); size = $file.Length; sha256 = Get-Digest $target }
    }
    Assert-Condition (@($manifest | Where-Object { $_.path -ceq 'result.json' }).Count -eq 1 -and @($manifest | Where-Object { $_.path -ceq 'cleanup.json' }).Count -eq 1) 'Required failure/success evidence missing'
    Write-Json (Join-Path $destination 'manifest.json') @{ commit = $script:Owner.commit; files = $manifest; sdkIncluded = $false; packagesIncluded = $false; productBinariesIncluded = $false }
    foreach ($item in $manifest) { Assert-Condition ((Get-Digest (Join-Path $destination $item.path)) -ceq $item.sha256) 'Published evidence hash mismatch' }
    if (-not [string]::IsNullOrEmpty($env:GITHUB_OUTPUT)) { Add-Content -LiteralPath $env:GITHUB_OUTPUT -Value 'publish_safe=true' -Encoding utf8 }
}
