# Read only the image identity sources; never export complete runner configuration or environment blocks.
function Find-RunnerSetupInfo(
    [int]$StartProcessId = $PID,
    [scriptblock]$ReadProcess = { param([int]$ProcessId) Get-CimInstance -ClassName Win32_Process -Filter "ProcessId = $ProcessId" -ErrorAction Stop }
) {
    $ancestorId = $StartProcessId
    $childCreated = $null
    $visited = [Collections.Generic.HashSet[int]]::new()
    try {
        for ($depth = 0; $depth -lt 16 -and $ancestorId -gt 0; $depth++) {
            if (-not $visited.Add($ancestorId)) { throw 'AncestorCycle' }
            $rows = @(& $ReadProcess $ancestorId)
            if ($rows.Count -ne 1) { throw 'AncestorUnavailable' }
            $row = $rows[0]
            if ($null -eq $row.CreationDate) { throw 'AncestorCreationMissing' }
            $created = $row.CreationDate.ToUniversalTime()
            if ($null -ne $childCreated -and $created -gt $childCreated) { throw 'AncestorPidReused' }
            if ($row.Name -ieq 'Runner.Worker.exe') {
                if ([string]::IsNullOrWhiteSpace($row.ExecutablePath)) { throw 'WorkerPathUnavailable' }
                $binary = [IO.Path]::GetFullPath($row.ExecutablePath)
                $bin = [IO.Path]::GetDirectoryName($binary)
                if ([IO.Path]::GetFileName($binary) -ine 'Runner.Worker.exe' -or [IO.Path]::GetFileName($bin) -ine 'bin') { throw 'UnexpectedWorkerLayout' }
                return @{
                    status = 'found'; path = Join-Path ([IO.Path]::GetDirectoryName($bin)) '.setup_info'
                    workerPid = $ancestorId; workerCreatedUtc = $created.ToString('O'); workerExecutable = $binary
                    errorCode = $null
                }
            }
            $childCreated = $created
            $ancestorId = [int]$row.ParentProcessId
        }
        throw 'WorkerAncestorNotFound'
    } catch {
        # Exception messages from CIM can contain unrelated machine data; retain only a bounded classification.
        $known = @('AncestorCycle', 'AncestorUnavailable', 'AncestorCreationMissing', 'AncestorPidReused', 'WorkerPathUnavailable', 'UnexpectedWorkerLayout', 'WorkerAncestorNotFound')
        $code = if ($_.Exception.Message -cin $known) { $_.Exception.Message } else { 'AncestorReadFailed' }
        return @{ status = 'unavailable'; path = $null; errorCode = $code }
    }
}

function ConvertFrom-RunnerImageMetadata([string]$Text) {
    $result = @{ status = 'invalid'; errorCode = $null; image = $null; version = $null; includedSoftware = $null; imageRelease = $null; selectedDetail = $null }
    try { $groups = ConvertFrom-Json -InputObject $Text -AsHashtable -NoEnumerate -ErrorAction Stop } catch {
        $result.errorCode = 'InvalidJson'; return $result
    }
    if ($groups -isnot [array]) { $result.errorCode = 'ExpectedGroupArray'; return $result }
    $images = [Collections.Generic.List[object]]::new()
    foreach ($group in $groups) {
        if ($group -isnot [Collections.IDictionary]) { continue }
        $groupKeys = @($group.Keys | Where-Object { $_ -ieq 'group' })
        if ($groupKeys.Count -gt 1) { $result.errorCode = 'DuplicateGroupField'; return $result }
        if ($groupKeys.Count -eq 1 -and $group[$groupKeys[0]] -ceq 'Runner Image') { $images.Add($group) }
    }
    if ($images.Count -ne 1) { $result.errorCode = 'RequireUniqueRunnerImageGroup'; return $result }
    $detailKeys = @($images[0].Keys | Where-Object { $_ -ieq 'detail' })
    if ($detailKeys.Count -ne 1 -or $images[0][$detailKeys[0]] -isnot [string]) { $result.errorCode = 'ImageDetailMissing'; return $result }
    $detail = $images[0][$detailKeys[0]]
    $lines = @($detail.TrimEnd([char[]]"`r`n") -split '\r?\n')
    if ($lines.Count -ne 4) { $result.errorCode = 'UnexpectedImageDetailLines'; return $result }
    $fields = @{}
    foreach ($line in $lines) {
        if ($line -cnotmatch '^(Image|Version|Included Software|Image Release): (.+)$') { $result.errorCode = 'UnexpectedImageDetailField'; return $result }
        $key = $Matches[1]; $value = $Matches[2]
        if ($fields.ContainsKey($key)) { $result.errorCode = 'DuplicateImageDetailField'; return $result }
        $fields[$key] = $value
    }
    # Validate before retaining selected text: unknown groups, keys, hosts and arbitrary values stay out of artifacts.
    if ($fields['Image'] -cne 'windows-2022' -or $fields['Version'] -cnotmatch '^\d{8}\.\d+\.\d+$' -or
        $fields['Included Software'] -cnotmatch '^https://github\.com/actions/runner-images/blob/win22/\d{8}\.\d+/images/windows/Windows2022-Readme\.md$' -or
        $fields['Image Release'] -cnotmatch '^https://github\.com/actions/runner-images/releases/tag/win22(?:/|%2F)\d{8}\.\d+$') {
        $result.errorCode = 'UnexpectedImageIdentityFormat'; return $result
    }
    $result.image = $fields['Image']; $result.version = $fields['Version']
    $result.includedSoftware = $fields['Included Software']; $result.imageRelease = $fields['Image Release']
    $result.selectedDetail = $detail
    $tag = $result.version.Substring(0, $result.version.LastIndexOf('.'))
    if ($result.includedSoftware -cne "https://github.com/actions/runner-images/blob/win22/$tag/images/windows/Windows2022-Readme.md" -or
        $result.imageRelease -cnotin @("https://github.com/actions/runner-images/releases/tag/win22/$tag", "https://github.com/actions/runner-images/releases/tag/win22%2F$tag")) {
        $result.errorCode = 'ImageVersionLinksDisagree'; return $result
    }
    $result.status = 'captured'
    return $result
}

function Read-RunnerImageMetadata([AllowNull()][string]$Path) {
    $result = @{ path = $Path; capturedUtc = [DateTime]::UtcNow.ToString('O'); status = 'unavailable'; errorCode = $null; size = $null; lastWriteUtc = $null; sha256 = $null; metadata = $null }
    if ([string]::IsNullOrWhiteSpace($Path)) { $result.errorCode = 'SourcePathUnavailable'; return $result }
    $stream = $null
    try {
        $item = Get-Item -LiteralPath $Path -Force -ErrorAction Stop
        if ($item.PSIsContainer -or ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)) { $result.errorCode = 'SourceNotRegularFile'; return $result }
        # Deny concurrent write/delete and bound both memory and the read to this exact opened file.
        $stream = [IO.File]::Open($Path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
        if ($stream.Length -gt 1MB) { $result.errorCode = 'SourceTooLarge'; return $result }
        $bytes = [byte[]]::new([int]$stream.Length)
        $stream.ReadExactly($bytes, 0, $bytes.Length)
        $result.size = $bytes.Length
        $result.lastWriteUtc = [IO.File]::GetLastWriteTimeUtc($Path).ToString('O')
        $result.sha256 = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
        $text = [Text.UTF8Encoding]::new($false, $true).GetString($bytes).TrimStart([char]0xfeff)
        $result.metadata = ConvertFrom-RunnerImageMetadata $text
        $result.status = $result.metadata.status
        $result.errorCode = $result.metadata.errorCode
    } catch {
        $result.errorCode = if ($_.Exception.GetBaseException() -is [Text.DecoderFallbackException]) { 'SourceNotUtf8' } else { 'SourceReadFailed' }
    } finally { if ($null -ne $stream) { $stream.Dispose() } }
    return $result
}

function Get-ImageIdentityAssessment([hashtable]$Sources) {
    $issues = [Collections.Generic.List[string]]::new()
    $process = $Sources.environment.Process
    foreach ($scope in @('Process', 'Machine', 'User')) {
        $entry = $Sources.environment[$scope]
        if ($entry.status -cne 'captured') { $issues.Add("environment.$scope.unavailable"); continue }
        # User overrides are optional; absence is recorded, while any present value must agree.
        if ($scope -cne 'User' -or $null -ne $entry.imageVersion) {
            if ($entry.imageVersion -cnotmatch '^\d{8}\.\d+\.\d+$') { $issues.Add("environment.$scope.invalidVersion") }
            elseif ($entry.imageVersion -cne $process.imageVersion) { $issues.Add("environment.$scope.versionMismatch") }
        }
        if ($scope -cne 'User' -or $null -ne $entry.imageOS) {
            if ($entry.imageOS -cne 'win22') { $issues.Add("environment.$scope.invalidOS") }
        }
    }
    foreach ($name in @('setupInfo', 'imageData')) {
        $entry = $Sources.files[$name]
        if ($entry.status -cne 'captured') { $issues.Add("files.$name.unresolved"); continue }
        if ($entry.metadata.version -cne $process.imageVersion) { $issues.Add("files.$name.versionMismatch") }
    }
    return @{ status = $(if ($issues.Count -eq 0) { 'consistent' } else { 'unresolved' }); issues = $issues.ToArray() }
}

function Get-RunnerImageIdentity {
    $report = @{
        schemaVersion = 1; capturedUtc = [DateTime]::UtcNow.ToString('O'); startupLogCompared = $false
        runId = $env:GITHUB_RUN_ID; attempt = $env:GITHUB_RUN_ATTEMPT; commit = $env:GITHUB_SHA
        workflowSha = $env:GITHUB_WORKFLOW_SHA
        setupInfoLocation = Find-RunnerSetupInfo; environment = @{}; files = @{}
    }
    foreach ($scope in @('Process', 'Machine', 'User')) {
        $entry = @{ status = 'unavailable'; imageOS = $null; imageVersion = $null; errorCode = $null }
        try {
            $target = [EnvironmentVariableTarget]$scope
            $entry.imageOS = [Environment]::GetEnvironmentVariable('ImageOS', $target)
            $entry.imageVersion = [Environment]::GetEnvironmentVariable('ImageVersion', $target)
            $entry.status = 'captured'
        } catch { $entry.errorCode = 'EnvironmentReadFailed' }
        $report.environment[$scope] = $entry
    }
    $report.files.setupInfo = Read-RunnerImageMetadata $report.setupInfoLocation.path
    $report.files.imageData = Read-RunnerImageMetadata 'C:\imagedata.json'
    $report.assessment = Get-ImageIdentityAssessment $report
    $path = Join-Path $script:Evidence 'image-identity.json'
    Write-Json $path $report
    return @{
        status = $report.assessment.status; issues = $report.assessment.issues
        processImageVersion = $report.environment.Process.imageVersion
        report = 'image-identity.json'; reportSha256 = Get-Digest $path
    }
}

function Assert-ConsistentImageIdentity([hashtable]$Inventory) {
    Assert-Condition ($Inventory.ContainsKey('imageIdentity') -and $null -ne $Inventory.imageIdentity -and
        $Inventory.imageIdentity.status -ceq 'consistent' -and $Inventory.imageIdentity.processImageVersion -ceq $Inventory.imageVersion) 'Consistent image identity required; inspect image-identity.json'
}
