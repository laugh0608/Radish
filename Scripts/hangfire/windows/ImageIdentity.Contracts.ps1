function New-ImageMetadataFixture([string]$Version = '20261026.347.1') {
    $tag = $Version.Substring(0, $Version.LastIndexOf('.'))
    return ConvertTo-Json -Depth 10 -InputObject @(
        @{ group = '_internal_fixture'; detail = 'DO_NOT_EXPORT_INTERNAL_SENTINEL' },
        @{ group = 'Runner Image'; detail = "Image: windows-2022`nVersion: $Version`nIncluded Software: https://github.com/actions/runner-images/blob/win22/$tag/images/windows/Windows2022-Readme.md`nImage Release: https://github.com/actions/runner-images/releases/tag/win22%2F$tag" }
    )
}

function New-ImageSourcesFixture {
    return @{
        environment = @{
            Process = @{ status = 'captured'; imageOS = 'win22'; imageVersion = '20261026.347.1' }
            Machine = @{ status = 'captured'; imageOS = 'win22'; imageVersion = '20261026.347.1' }
            User = @{ status = 'captured'; imageOS = $null; imageVersion = $null }
        }
        files = @{
            setupInfo = @{ status = 'captured'; metadata = ConvertFrom-RunnerImageMetadata (New-ImageMetadataFixture) }
            imageData = @{ status = 'captured'; metadata = ConvertFrom-RunnerImageMetadata (New-ImageMetadataFixture) }
        }
    }
}

function Invoke-ImageIdentityContracts([string]$FixtureRoot, [hashtable]$Report) {
    $path = Assert-OwnedPath (Join-Path $FixtureRoot 'image-metadata.json')
    [IO.File]::WriteAllText($path, (New-ImageMetadataFixture), [Text.UTF8Encoding]::new($false))
    $file = Read-RunnerImageMetadata $path
    Assert-Condition ($file.status -ceq 'captured' -and $file.sha256 -ceq (Get-Digest $path) -and $file.size -eq (Get-Item -LiteralPath $path).Length -and $null -ne $file.lastWriteUtc) 'Image file identity was not retained'
    Assert-Condition (($file | ConvertTo-Json -Depth 20) -cnotmatch 'DO_NOT_EXPORT_INTERNAL_SENTINEL') 'Unselected runner metadata leaked'
    $capitalized = ConvertFrom-RunnerImageMetadata ((New-ImageMetadataFixture) -creplace '"group":', '"Group":' -creplace '"detail":', '"Detail":')
    Assert-Condition ($capitalized.status -ceq 'captured' -and $capitalized.version -ceq $file.metadata.version) 'Known JSON property casing changed identity'
    $Report.cases += 'image-file-hash-and-whitelist'

    $invalid = ConvertFrom-RunnerImageMetadata '{invalid-json'
    Assert-Condition ($invalid.status -ceq 'invalid' -and $invalid.errorCode -ceq 'InvalidJson' -and $null -eq $invalid.selectedDetail) 'Invalid image JSON accepted'
    $groups = ConvertFrom-Json (New-ImageMetadataFixture) -AsHashtable -NoEnumerate
    $duplicate = ConvertFrom-RunnerImageMetadata (ConvertTo-Json -Depth 10 -InputObject @($groups[1], $groups[1]))
    Assert-Condition ($duplicate.errorCode -ceq 'RequireUniqueRunnerImageGroup') 'Duplicate image group accepted'
    $Report.cases += 'image-invalid-json-and-duplicate-group'

    $groups[1].detail += "`nUnexpected: DO_NOT_EXPORT_EXTRA_SENTINEL"
    $extra = ConvertFrom-RunnerImageMetadata (ConvertTo-Json -Depth 10 -InputObject $groups)
    Assert-Condition ($extra.status -ceq 'invalid' -and ($extra | ConvertTo-Json) -cnotmatch 'DO_NOT_EXPORT_EXTRA_SENTINEL') 'Unexpected image fields leaked'
    $Report.cases += 'image-extra-fields-rejected'

    $links = ConvertFrom-RunnerImageMetadata ((New-ImageMetadataFixture) -replace 'win22%2F20261026.347', 'win22%2F20261025.346')
    Assert-Condition ($links.status -ceq 'invalid' -and $links.errorCode -ceq 'ImageVersionLinksDisagree' -and $links.version -ceq '20261026.347.1' -and $links.imageRelease.EndsWith('20261025.346')) 'Conflicting release link was normalized or accepted'
    $Report.cases += 'image-link-version-mismatch'

    $missing = Read-RunnerImageMetadata (Join-Path $FixtureRoot 'absent-image-metadata.json')
    Assert-Condition ($missing.status -ceq 'unavailable' -and $null -eq $missing.sha256 -and $null -ne $missing.errorCode) 'Missing image file accepted'
    [IO.File]::WriteAllBytes($path, [byte[]]::new(1MB + 1))
    $large = Read-RunnerImageMetadata $path
    Assert-Condition ($large.status -ceq 'unavailable' -and $large.errorCode -ceq 'SourceTooLarge') 'Oversized image file accepted'
    [IO.File]::WriteAllBytes($path, [byte[]]@(0xff, 0xff))
    $encoding = Read-RunnerImageMetadata $path
    Assert-Condition ($encoding.status -ceq 'unavailable' -and $encoding.errorCode -ceq 'SourceNotUtf8' -and $null -ne $encoding.sha256) 'Invalid encoding accepted or source hash lost'
    $Report.cases += 'image-missing-oversized-and-invalid-encoding'

    $sources = New-ImageSourcesFixture
    $assessment = Get-ImageIdentityAssessment $sources
    Assert-Condition ($assessment.status -ceq 'consistent' -and $assessment.issues.Count -eq 0) 'Consistent image sources rejected'
    $accepted = @{ complete = $true; imageVersion = '20261026.347.1'; imageIdentity = @{ status = $assessment.status; processImageVersion = '20261026.347.1' } }
    Assert-ConsistentImageIdentity $accepted
    $Report.cases += 'image-consistent-with-absent-user-overrides'

    foreach ($name in @('setupInfo', 'imageData')) {
        $sources = New-ImageSourcesFixture
        $sources.files[$name].metadata = ConvertFrom-RunnerImageMetadata (New-ImageMetadataFixture '20261025.346.1')
        $assessment = Get-ImageIdentityAssessment $sources
        Assert-Condition ($assessment.status -ceq 'unresolved' -and "files.$name.versionMismatch" -cin $assessment.issues) 'Image file disagreement accepted'
    }
    $Report.cases += 'image-setup-and-data-disagreement'

    foreach ($scope in @('Machine', 'User')) {
        $sources = New-ImageSourcesFixture
        $sources.environment[$scope].imageVersion = '20261025.346.1'
        $assessment = Get-ImageIdentityAssessment $sources
        Assert-Condition ($assessment.status -ceq 'unresolved' -and "environment.$scope.versionMismatch" -cin $assessment.issues) 'Environment override disagreement accepted'
    }
    $Report.cases += 'image-machine-and-user-disagreement'

    $sources = New-ImageSourcesFixture
    $sources.environment.Machine.imageVersion = $null
    $sources.files.setupInfo.status = 'unavailable'
    $assessment = Get-ImageIdentityAssessment $sources
    Assert-Condition ($assessment.status -ceq 'unresolved' -and 'environment.Machine.invalidVersion' -cin $assessment.issues -and 'files.setupInfo.unresolved' -cin $assessment.issues) 'Missing required identity source accepted'
    $sources.environment.Process.imageOS = 'win25'
    Assert-Condition ('environment.Process.invalidOS' -cin (Get-ImageIdentityAssessment $sources).issues) 'Different image family accepted'
    $Report.cases += 'image-required-source-and-os-rejection'

    $message = 'Consistent image identity required; inspect image-identity.json'
    Assert-ContractRejection { Assert-ReviewedInventory @{ complete = $true } 'image' 'vs' 'msbuild' ('a' * 64) } $message
    $accepted.imageIdentity.status = 'unresolved'
    Assert-ContractRejection { Assert-ConsistentImageIdentity $accepted } $message
    $accepted.imageIdentity.status = 'consistent'; $accepted.imageVersion = '20261025.346.1'
    Assert-ContractRejection { Assert-ConsistentImageIdentity $accepted } $message
    $Report.cases += 'image-unreviewable-inventory-rejected'

    $processes = @{
        101 = [pscustomobject]@{ ProcessId = 101; ParentProcessId = 100; Name = 'pwsh.exe'; CreationDate = [DateTime]::Parse('2026-10-31T10:00:01Z') }
        100 = [pscustomobject]@{ ProcessId = 100; ParentProcessId = 99; Name = 'Runner.Worker.exe'; ExecutablePath = 'C:\fixture\runner\bin\Runner.Worker.exe'; CreationDate = [DateTime]::Parse('2026-10-31T10:00:00Z') }
    }
    $lookup = { param([int]$ProcessId) $processes[$ProcessId] }.GetNewClosure()
    $location = Find-RunnerSetupInfo -StartProcessId 101 -ReadProcess $lookup
    Assert-Condition ($location.status -ceq 'found' -and $location.path -ceq 'C:\fixture\runner\.setup_info' -and $location.workerPid -eq 100) 'Worker ancestor location differs'
    $Report.cases += 'image-worker-ancestor-location'

    $processes[100].CreationDate = [DateTime]::Parse('2026-10-31T10:00:02Z')
    $location = Find-RunnerSetupInfo -StartProcessId 101 -ReadProcess $lookup
    Assert-Condition ($location.status -ceq 'unavailable' -and $location.errorCode -ceq 'AncestorPidReused') 'Reused ancestor PID accepted'
    $missingWorker = Find-RunnerSetupInfo -StartProcessId 101 -ReadProcess { param([int]$ProcessId) }
    Assert-Condition ($missingWorker.status -ceq 'unavailable' -and $null -eq $missingWorker.path) 'Missing worker path fabricated'
    $Report.cases += 'image-ancestor-reuse-and-missing-rejection'
}
