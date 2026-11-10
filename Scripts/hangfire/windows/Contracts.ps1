# These checks exercise our own PowerShell helpers only, before any real tool discovery or upstream task.
function Assert-ContractRejection([scriptblock]$Action, [string]$Message) {
    $caught = $false
    try { & $Action | Out-Null } catch {
        $caught = $true
        Assert-Condition ($_.Exception.Message -ceq $Message) "Unexpected contract failure: $($_.Exception.Message)"
    }
    Assert-Condition $caught "Contract did not reject: $Message"
}

function Invoke-PowerShellContracts {
    $fixtureRoot = Assert-OwnedPath (Join-Path $script:Work 'powershell-contracts')
    Assert-Condition (-not (Test-Path -LiteralPath $fixtureRoot)) 'Contract directory already exists'
    [IO.Directory]::CreateDirectory($fixtureRoot) | Out-Null
    $report = @{ status = 'running'; fixtureOnly = $true; cases = @(); fixturesRemoved = $false }
    $reportPath = Join-Path $script:Evidence 'powershell-contracts.json'
    Write-Json $reportPath $report
    try {
        $pwsh = Join-Path $PSHOME 'pwsh.exe'
        $extraEnvironment = @{ Name = 'not-a-command-name'; RADISH_CONTRACT_SENTINEL = 'contract-sentinel' }
        $output = Invoke-OwnedCommand $pwsh @('-NoLogo', '-NoProfile', '-NonInteractive', '-Command', '[Console]::Write($env:RADISH_CONTRACT_SENTINEL); exit 0') 'contract-success' -Directory $fixtureRoot -TimeoutSeconds 30 -Environment $extraEnvironment
        $record = $script:Owner.commands[-1]
        Assert-Condition ($output -ceq 'contract-sentinel' -and $record.name -ceq 'contract-success' -and $record.exitCode -eq 0 -and $record.completed) 'Command identity or environment changed after successful child'
        Assert-Condition ((Split-Path -Leaf $record.log) -cmatch '^\d+-contract-success\.log$') 'Success log name differs from command identity'
        $report.cases += 'command-success-name-and-environment'

        $caught = $false
        try {
            Invoke-OwnedCommand $pwsh @('-NoLogo', '-NoProfile', '-NonInteractive', '-Command', '[Console]::Write("expected failure"); exit 23') 'contract-exit-23' -Directory $fixtureRoot -TimeoutSeconds 30 -Environment $extraEnvironment | Out-Null
        } catch {
            $caught = $true
            $record = $script:Owner.commands[-1]
            Assert-Condition ($_.Exception.Message -ceq "Command failed: contract-exit-23; see $(Join-Path $script:Evidence $record.log)") 'Failure message lost command identity'
        }
        $record = $script:Owner.commands[-1]
        Assert-Condition ($caught -and $record.name -ceq 'contract-exit-23' -and $record.exitCode -eq 23 -and $record.completed) 'Failed child did not retain command identity / exit code'
        Assert-Condition ((Split-Path -Leaf $record.log) -cmatch '^\d+-contract-exit-23\.log$') 'Failure log name differs from command identity'
        Assert-Condition ((Get-Content -LiteralPath (Join-Path $script:Evidence $record.log) -Raw) -ceq 'expected failure') 'Expected child failure log missing'
        $report.cases += 'command-failure-name-and-exit-code'

        # Synthetic VS records live only in work/ and cannot become production inventory.json.
        $vs17 = @{ installationVersion = '17.14.1.0'; installationPath = 'C:\fixture\VS2022' }
        $vs18 = @{ installationVersion = '18.0.1.0'; installationPath = 'C:\fixture\VS2026' }
        foreach ($case in @(
            @{ name = 'zero-vs'; json = ConvertTo-Json -InputObject @(); count = 0; error = 'Require exactly one eligible VS 2022 instance' },
            @{ name = 'one-vs'; json = ConvertTo-Json -InputObject @($vs17); count = 1; error = $null },
            @{ name = 'two-vs'; json = ConvertTo-Json -InputObject @($vs17, $vs17); count = 2; error = 'Require exactly one eligible VS 2022 instance' },
            @{ name = 'wrong-vs-major'; json = ConvertTo-Json -InputObject @($vs18); count = 1; error = 'Selected instance is not VS 2022' }
        )) {
            $inventory = @{ complete = $false; stage = 'fixture'; vswhere = @{}; visualStudio = $null }
            $progressPath = Join-Path $fixtureRoot "$($case.name).json"
            if ($null -eq $case.error) {
                $selected = Read-VisualStudioInventory $inventory $case.json $progressPath
                Assert-Condition ($selected.installationVersion -ceq $vs17.installationVersion -and $inventory.visualStudio.version -ceq $vs17.installationVersion) 'Unique VS 2022 selection changed'
            } else {
                Assert-ContractRejection { Read-VisualStudioInventory $inventory $case.json $progressPath } $case.error
            }
            $saved = Read-Json $progressPath
            Assert-Condition ($saved.complete -eq $false -and $saved.vswhere.rawOutput -ceq $case.json -and @($saved.vswhere.instances).Count -eq $case.count) 'Incomplete discovery evidence was lost'
            if ($null -ne $case.error) { Assert-Condition ($null -eq $saved.visualStudio) 'Rejected VS was selected' }
            $report.cases += $case.name
        }

        $inventory = @{ complete = $false; stage = 'fixture'; vswhere = @{}; visualStudio = $null }
        $progressPath = Join-Path $fixtureRoot 'invalid-json.json'
        $caught = $false
        try { Read-VisualStudioInventory $inventory '{invalid-json' $progressPath | Out-Null } catch { $caught = $true }
        $saved = Read-Json $progressPath
        Assert-Condition ($caught -and $saved.complete -eq $false -and $saved.vswhere.rawOutput -ceq '{invalid-json') 'Invalid discovery response was not retained and rejected'
        $report.cases += 'invalid-json-evidence'

        Assert-ContractRejection { Assert-ReviewedInventory @{ complete = $false } 'image' 'vs' 'msbuild' ('a' * 64) } 'Complete inventory required for verify'
        $report.cases += 'incomplete-inventory-rejected-for-verify'
        Assert-Condition (-not (Test-Path -LiteralPath (Join-Path $script:Evidence 'inventory.json'))) 'Contracts produced a real inventory snapshot'
        Invoke-ImageIdentityContracts $fixtureRoot $report
        $report.status = 'passed'
    } catch {
        $report.status = 'failed'
        $report.error = $_.Exception.Message
        throw
    } finally {
        try {
            Remove-Owned $fixtureRoot
            $report.fixturesRemoved = -not (Test-Path -LiteralPath $fixtureRoot)
        } catch {
            $report.status = 'failed'
            $report.cleanupError = $_.Exception.Message
            throw
        } finally { Write-Json $reportPath $report }
    }
}
