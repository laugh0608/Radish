function Invoke-WindowsVerification($Inventory) {
    Initialize-VerificationInputs
    $records = @{}
    foreach ($rootName in @('a', 'b')) {
        $base = Join-Path $script:Work "replay/$rootName"
        $packages = Join-Path $base 'packages'
        $cronos = Join-Path $base 'cronos'
        $evidence = Join-Path $script:Evidence "matrix/$rootName"
        New-SourceClone 'cronos' $cronos
        Invoke-Tool @('adapt', $cronos, 'cronos', (Join-Path $evidence 'cronos')) "adapt-$rootName-cronos"
        Set-CanonicalTargets $cronos (Join-Path $base 'checked-in/hangfire') $cronos $packages $false
        Build-SelectedProject (Join-Path $cronos 'src/Cronos/Cronos.csproj') $packages (Join-Path $evidence 'cronos')
        $records[$rootName] = @{}
        foreach ($variant in $script:Inputs.variants) {
            $hangfire = Join-Path $base "$variant/hangfire"
            $groupEvidence = Join-Path $evidence $variant
            New-SourceClone 'hangfire' $hangfire
            Invoke-Tool @('adapt', $hangfire, 'hangfire', $groupEvidence) "adapt-$rootName-$variant"
            Invoke-RazorGeneration $hangfire $variant $groupEvidence $Inventory.msbuild.path
            Set-CanonicalTargets $hangfire $hangfire $cronos $packages $true
            $project = Join-Path $hangfire 'src/Hangfire.Core/Hangfire.Core.csproj'
            Build-SelectedProject $project $packages $groupEvidence
            $bin = Join-Path $hangfire 'src/Hangfire.Core/bin/Release/netstandard2.0'
            $output = Join-Path $base "$variant/merged"
            [IO.Directory]::CreateDirectory($output) | Out-Null
            $dll = Join-Path $output 'Hangfire.Core.dll'
            Invoke-OwnedCommand $script:Owner.dotnet @((Join-Path $script:IlRepack 'tools/ILRepack.exe'), "/out:$dll", "/lib:$bin", '/target:library', '/internalize', (Join-Path $bin 'Hangfire.Core.dll'), (Join-Path $cronos 'src/Cronos/bin/Release/netstandard2.0/Cronos.dll')) "merge-$rootName-$variant" | Out-Null
            $metadata = Join-Path $groupEvidence 'assembly.json'
            Invoke-Tool @('assembly', $dll, $metadata) "metadata-$rootName-$variant"
            Invoke-Tool @('proof', $metadata, $hangfire, $cronos, $packages, (Join-Path $groupEvidence 'source-proof.json')) "source-proof-$rootName-$variant"
            $diff = Invoke-OwnedCommand $script:Git @('-C', $hangfire, 'diff', '--binary') "diff-$rootName-$variant"
            [IO.File]::WriteAllText((Join-Path $groupEvidence 'tracked-changes.patch'), $diff, [Text.UTF8Encoding]::new($false))
            $records[$rootName][$variant] = @{ repository = $hangfire; dll = $dll; evidence = $groupEvidence; metadata = Read-Json $metadata }
        }
        $r = $records[$rootName]
        Invoke-Tool @('compare', $r['checked-in'].repository, $r['regenerated-lf'].repository, $r['regenerated-crlf'].repository, $r['marked-crlf'].repository, (Join-Path $evidence 'generated-token-comparison.json')) "compare-generated-$rootName"
    }
    foreach ($variant in $script:Inputs.variants) {
        $a = $records.a[$variant]
        $b = $records.b[$variant]
        Assert-Condition ($a.metadata.sha256 -ceq $b.metadata.sha256 -and $a.metadata.mvid -ceq $b.metadata.mvid) 'Clean-directory DLL replay differs'
        $aFiles = @(Read-Json (Join-Path $a.evidence 'generated.json'))
        $bFiles = @(Read-Json (Join-Path $b.evidence 'generated.json'))
        Assert-Condition (($aFiles | ConvertTo-Json -Depth 10 -Compress) -ceq ($bFiles | ConvertTo-Json -Depth 10 -Compress)) 'Clean-directory generated-source replay differs'
    }
    $licenseDirectory = Join-Path $script:Evidence 'licenses'
    [IO.Directory]::CreateDirectory($licenseDirectory) | Out-Null
    foreach ($name in @('COPYING', 'COPYING.LESSER')) { Copy-Item -LiteralPath (Join-Path $records.a['checked-in'].repository $name) -Destination (Join-Path $licenseDirectory $name) }
    $cronosLicense = Join-Path $script:Work 'replay/a/cronos/LICENSE'
    Copy-Item -LiteralPath $cronosLicense -Destination (Join-Path $licenseDirectory 'Cronos-LICENSE.txt')
    $render = Invoke-RenderMatrix $records
    Write-Json (Join-Path $script:Evidence 'verification.json') @{ complete = $true; sourceTargets = @{ cronos = 2; core = 8 }; generatedPairs = 23; replayByteEqual = $true; renderGroups = $render.Count; results = $render; ports = @(); database = $null }
}
function Invoke-RenderMatrix($Records) {
    $probe = Join-Path $script:Work 'probe'
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Probe') -Destination $probe -Recurse
    $project = Join-Path $probe 'RazorProbe.csproj'
    $json = Join-Path $script:Work 'replay/a/packages/newtonsoft.json/13.0.4/lib/net6.0/Newtonsoft.Json.dll'
    Assert-Condition ((Get-Digest $json) -ceq $script:Inputs.jsonRuntime.sha256) 'Pinned JSON runtime missing or changed'
    $properties = @("-p:CoreReferencePath=$($Records.a['checked-in'].dll)", "-p:JsonReferencePath=$json")
    Invoke-OwnedCommand $script:Owner.dotnet (@('restore', $project, '--configfile', $script:Config, '--warnaserror') + $properties) 'render-probe-restore' | Out-Null
    $results = @()
    foreach ($configuration in @('Debug', 'Release')) {
        Invoke-OwnedCommand $script:Owner.dotnet (@('build', $project, '-c', $configuration, '--no-restore', '--warnaserror', '-p:UseSharedCompilation=false') + $properties) "probe-build-$configuration" | Out-Null
        $probeOutput = Join-Path $probe "bin/$configuration/net10.0"
        foreach ($rootName in @('a', 'b')) {
            foreach ($variant in $script:Inputs.variants) {
                $entry = $Records[$rootName][$variant]
                $case = Join-Path $script:Work "cases/$rootName-$variant-$configuration"
                [IO.Directory]::CreateDirectory($case) | Out-Null
                foreach ($suffix in @('.dll', '.deps.json', '.runtimeconfig.json')) { Copy-Item -LiteralPath (Join-Path $probeOutput "WindowsRazorProbe$suffix") -Destination $case }
                Copy-Item -LiteralPath $entry.dll -Destination (Join-Path $case 'Hangfire.Core.dll')
                Copy-Item -LiteralPath $json -Destination (Join-Path $case 'Newtonsoft.Json.dll')
                $report = Join-Path $script:Evidence "render/$rootName-$variant-$configuration.json"
                [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($report)) | Out-Null
                $mode = if ($variant -eq 'marked-crlf') { 'marked' } else { 'plain' }
                Invoke-OwnedCommand $script:Owner.dotnet @((Join-Path $case 'WindowsRazorProbe.dll'), $report, $entry.metadata.sha256, $mode, $script:Inputs.marker, $script:Inputs.jsonRuntime.sha256) "render-$rootName-$variant-$configuration" -TimeoutSeconds 30 | Out-Null
                $result = Read-Json $report
                Assert-Condition ($result.sha256 -ceq $entry.metadata.sha256 -and $result.assembly -ieq (Join-Path $case 'Hangfire.Core.dll')) 'Render report uses a different Core'
                $results += @{ root = $rootName; variant = $variant; configuration = $configuration; result = $result }
            }
        }
    }
    Assert-Condition ($results.Count -eq 16 -and @($results.result.normalizedHtmlSha256 | Select-Object -Unique).Count -eq 1) 'Missing render cases or unexplained fragment differences'
    return $results
}
