function Get-ReviewedFile([string]$Url, [string]$Path, [string]$Digest, [string]$Algorithm = 'SHA256') {
    Assert-OwnedPath $Path | Out-Null
    Assert-Condition (-not (Test-Path -LiteralPath $Path)) 'Input destination exists'
    Assert-Condition ($Url.StartsWith('https://api.nuget.org/v3-flatcontainer/', [StringComparison]::Ordinal) -or $Url.StartsWith('https://builds.dotnet.microsoft.com/dotnet/Sdk/10.0.403/', [StringComparison]::Ordinal)) 'Unexpected input origin'
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($Path)) | Out-Null
    Invoke-WebRequest -Uri $Url -OutFile ($Path + '.partial') -TimeoutSec 600
    Assert-Condition ((Get-Digest ($Path + '.partial') $Algorithm) -ceq $Digest) 'Input hash differs'
    Move-Item -LiteralPath ($Path + '.partial') -Destination $Path
}
function Expand-ReviewedZip([string]$Archive, [string]$Destination) {
    Assert-OwnedPath $Destination | Out-Null
    Assert-Condition (-not (Test-Path -LiteralPath $Destination)) 'Extraction destination exists'
    $zip = [IO.Compression.ZipFile]::OpenRead($Archive)
    try {
        foreach ($entry in $zip.Entries) {
            $name = $entry.FullName.Replace('\', '/')
            Assert-Condition (-not [IO.Path]::IsPathRooted($name) -and $name -notmatch '(^|/)\.\.?(/|$)|:|\x00') 'Unsafe archive path'
            Assert-OwnedPath (Join-Path $Destination $entry.FullName) | Out-Null
        }
    } finally { $zip.Dispose() }
    [IO.Compression.ZipFile]::ExtractToDirectory($Archive, $Destination)
}
function Initialize-VerificationInputs {
    Assert-Condition ((Get-PSDrive -Name ([IO.Path]::GetPathRoot($script:Work).Substring(0, 1))).Free -ge 8GB) 'Less than 8 GiB free'
    $sdkZip = Join-Path $script:Work 'inputs/sdk.zip'
    Get-ReviewedFile $script:Inputs.sdk.url $sdkZip $script:Inputs.sdk.sha512 'SHA512'
    $sdk = Join-Path $script:Work 'sdk'
    Expand-ReviewedZip $sdkZip $sdk
    $script:Owner.dotnet = Join-Path $sdk 'dotnet.exe'
    Save-Owner
    Assert-Condition ((Invoke-OwnedCommand $script:Owner.dotnet @('--version') 'private-sdk-version').Trim() -ceq '10.0.403') 'Wrong SDK'
    $info = Invoke-OwnedCommand $script:Owner.dotnet @('--info') 'private-sdk-info'
    Assert-Condition ($info.Contains('10.0.14') -and $info -match 'Architecture:\s+x64') 'Wrong private runtime / architecture'
    Write-Json (Join-Path $script:Work 'global.json') @{ sdk = @{ version = '10.0.403'; rollForward = 'disable'; allowPrerelease = $false } }
    $script:Feed = Join-Path $script:Work 'feed'
    foreach ($package in $script:Inputs.packages) {
        Get-ReviewedFile $package.url (Join-Path $script:Feed "$($package.package).$($package.version).nupkg") $package.archiveSha256
    }
    $config = Join-Path $script:Work 'source.NuGet.Config'
    $feedXml = [Security.SecurityElement]::Escape($script:Feed)
    [IO.File]::WriteAllText($config, "<configuration><packageSources><clear/><add key=`"sealed-feed`" value=`"$feedXml`"/></packageSources><auditSources><clear/><add key=`"nuget.org`" value=`"https://api.nuget.org/v3/index.json`"/></auditSources></configuration>`n", [Text.UTF8Encoding]::new($false))
    $script:Config = $config
    $toolchain = Join-Path $script:Work 'toolchain/hangfire'
    [IO.Directory]::CreateDirectory($toolchain) | Out-Null
    Copy-Item -LiteralPath $PSScriptRoot -Destination (Join-Path $toolchain 'windows') -Recurse
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot '../Metadata') -Destination (Join-Path $toolchain 'Metadata') -Recurse
    $project = Join-Path $toolchain 'windows/Tools/WindowsRazorTools.csproj'
    Invoke-OwnedCommand $script:Owner.dotnet @('restore', $project, '--configfile', $config, '--warnaserror') 'tools-restore' | Out-Null
    Invoke-OwnedCommand $script:Owner.dotnet @('build', $project, '-c', 'Release', '--no-restore', '--warnaserror', '-p:UseSharedCompilation=false') 'tools-build' | Out-Null
    $script:Tool = Join-Path $toolchain 'windows/Tools/bin/Release/net10.0/WindowsRazorTools.dll'
    Invoke-Tool @('self-test', (Join-Path $script:Evidence 'tool-contracts.json')) 'tools-self-test'
    $contentReport = Join-Path $script:Evidence 'package-content-hashes.json'
    Invoke-Tool @('packages', $script:Feed, $contentReport) 'package-content-hashes'
    $contents = @(Read-Json $contentReport)
    Assert-Condition ($contents.Count -eq 11) 'Feed must contain exactly 11 packages'
    foreach ($item in $contents) {
        $match = @($script:Inputs.packages | Where-Object { $_.package -ceq $item.package -and $_.version -ceq $item.version })
        Assert-Condition ($match.Count -eq 1 -and $match[0].contentHash -ceq $item.contentHash -and $match[0].archiveSha256 -ceq $item.sha256) 'NuGet content identity mismatch'
    }
    $script:IlRepack = Join-Path $script:Work 'tools/ilrepack'
    Expand-ReviewedZip (Join-Path $script:Feed 'ilrepack.2.0.27.nupkg') $script:IlRepack
    $script:Git = (Get-Command git -CommandType Application).Source
    foreach ($name in @('hangfire', 'cronos')) {
        $source = $script:Inputs.repositories[$name]
        $bare = Join-Path $script:Work "inputs/$name.git"
        Invoke-OwnedCommand $script:Git @('init', '--bare', $bare) "init-$name" | Out-Null
        Invoke-OwnedCommand $script:Git @('-C', $bare, 'remote', 'add', 'origin', "https://github.com/$($source.repository).git") "origin-$name" | Out-Null
        Invoke-OwnedCommand $script:Git @('-C', $bare, 'fetch', '--depth=1', 'origin', $source.commit) "fetch-$name" | Out-Null
        Invoke-OwnedCommand $script:Git @('-C', $bare, 'fsck', '--full') "fsck-$name" | Out-Null
        $tree = (Invoke-OwnedCommand $script:Git @('-C', $bare, 'rev-parse', "$($source.commit)^{tree}") "tree-$name").Trim()
        Assert-Condition ($tree -ceq $source.treeSha) 'Source tree mismatch'
        $entries = (Invoke-OwnedCommand $script:Git @('-C', $bare, 'ls-tree', '-r', $source.commit) "entries-$name").Trim() -split '\r?\n'
        Assert-Condition ($entries.Count -eq $source.blobCount -and @($entries | Where-Object { $_ -notmatch '^100(644|755) blob ' }).Count -eq 0) 'Source modes / blobs mismatch'
        Write-Json (Join-Path $script:Evidence "input-$name.json") @{ commit = $source.commit; tree = $tree; blobs = $entries.Count; shallow = $true }
    }
}
function Invoke-Tool([string[]]$Arguments, [string]$Name) {
    Invoke-OwnedCommand $script:Owner.dotnet (@($script:Tool) + $Arguments) $Name | Out-Null
}
