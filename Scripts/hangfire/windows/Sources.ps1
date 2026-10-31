function New-SourceClone([string]$Name, [string]$Destination) {
    Assert-OwnedPath $Destination | Out-Null
    $bare = Join-Path $script:Work "inputs/$Name.git"
    Invoke-OwnedCommand $script:Git @('-c', 'core.autocrlf=false', 'clone', '--no-hardlinks', '--no-checkout', $bare, $Destination) "clone-$Name" | Out-Null
    Invoke-OwnedCommand $script:Git @('-C', $Destination, 'config', 'core.autocrlf', 'false') "line-endings-$Name" | Out-Null
    Invoke-OwnedCommand $script:Git @('-C', $Destination, 'checkout', '--detach', $script:Inputs.repositories[$Name].commit) "checkout-$Name" | Out-Null
    Invoke-OwnedCommand $script:Git @('-C', $Destination, 'remote', 'set-url', 'origin', "https://github.com/$($script:Inputs.repositories[$Name].repository).git") "source-origin-$Name" | Out-Null
}
function Set-CanonicalTargets([string]$Repository, [string]$Hangfire, [string]$Cronos, [string]$Packages, [bool]$EmbedPages) {
    $Hangfire = [IO.Path]::GetFullPath($Hangfire)
    $Cronos = [IO.Path]::GetFullPath($Cronos)
    $Packages = [IO.Path]::GetFullPath($Packages)
    foreach ($item in @($Hangfire, $Cronos, $Packages)) { Assert-Condition ($item -notmatch '[,=]') 'Unrepresentable PathMap root' }
    $h = [Security.SecurityElement]::Escape($Hangfire)
    $c = [Security.SecurityElement]::Escape($Cronos)
    $p = [Security.SecurityElement]::Escape($Packages)
    $embedding = if ($EmbedPages) { '<Target Name="EmbedReviewedRazor" BeforeTargets="CoreCompile"><ItemGroup><EmbeddedFiles Include="$(MSBuildProjectDirectory)/Dashboard/Pages/*.cshtml.cs"/><EmbeddedFiles Include="$(MSBuildProjectDirectory)/Dashboard/JsonStats.cs"/></ItemGroup></Target>' } else { '' }
    $xml = @"
<Project><PropertyGroup><PathMap>$h=/_/sources/hangfire,$c=/_/sources/cronos,$p=/_/packages</PathMap></PropertyGroup>
<Target Name="CanonicalReviewedSourceRoots" AfterTargets="InitializeSourceRootMappedPaths" BeforeTargets="_GenerateSourceLinkFile"><ItemGroup>
<SourceRoot><MappedPath Condition="'%(SourceRoot.Identity)' == '$h\'">/_/sources/hangfire/</MappedPath></SourceRoot>
<SourceRoot><MappedPath Condition="'%(SourceRoot.Identity)' == '$c\'">/_/sources/cronos/</MappedPath></SourceRoot>
</ItemGroup></Target>$embedding</Project>
"@
    [IO.File]::WriteAllText((Join-Path $Repository 'Directory.Build.targets'), $xml + "`n", [Text.UTF8Encoding]::new($false))
}
function Assert-BuildGraph([string]$Project, [string]$Evidence) {
    $directory = [IO.Path]::GetDirectoryName($Project)
    $assetsPath = Join-Path $directory 'obj/project.assets.json'
    $assets = Read-Json $assetsPath
    $allowed = @($script:Inputs.packages | ForEach-Object { "$($_.package)/$($_.version)" })
    $identities = @($assets.libraries.GetEnumerator() | Where-Object { $_.Value.type -eq 'package' } | ForEach-Object { $_.Key.ToLowerInvariant() })
    foreach ($framework in $assets.project.frameworks.Values) {
        if ($framework.ContainsKey('downloadDependencies')) {
            foreach ($download in $framework.downloadDependencies) {
                Assert-Condition ($download.version -match '^\[([^,\]]+)(?:,\s*\1)?\]$') 'Unpinned download-only input'
                $identities += "$($download.name.ToLowerInvariant())/$($Matches[1])"
            }
        }
    }
    Assert-Condition ($identities.Count -gt 0 -and @($identities | Where-Object { $_ -cnotin $allowed }).Count -eq 0) 'Unapproved source package graph'
    Copy-Item -LiteralPath $assetsPath -Destination (Join-Path $Evidence 'project.assets.json')
    Copy-Item -LiteralPath (Join-Path $directory 'packages.lock.json') -Destination (Join-Path $Evidence 'packages.lock.json')
}
function Build-SelectedProject([string]$Project, [string]$Packages, [string]$Evidence) {
    [IO.Directory]::CreateDirectory($Evidence) | Out-Null
    $environment = @{ NUGET_PACKAGES = $Packages }
    Invoke-OwnedCommand $script:Owner.dotnet @('restore', $Project, '--configfile', $script:Config, '--use-lock-file', '--force-evaluate', '--warnaserror', '-p:NuGetAuditMode=all') 'source-restore' -Environment $environment | Out-Null
    Assert-BuildGraph $Project $Evidence
    Invoke-OwnedCommand $script:Owner.dotnet @('restore', $Project, '--configfile', $script:Config, '--locked-mode', '--warnaserror', '-p:NuGetAuditMode=all') 'source-locked' -Environment $environment | Out-Null
    Invoke-OwnedCommand $script:Owner.dotnet @('build', $Project, '-c', 'Release', '--no-restore', '--warnaserror', '-p:UseSharedCompilation=false') 'source-build' -Environment $environment | Out-Null
}
function Assert-RazorSnapshot([string]$File, [string]$PackageDirectory, [string]$FullMsbuild) {
    [xml]$snapshot = Get-Content -LiteralPath $File -Raw
    Assert-Condition ($snapshot.snapshot.clr.StartsWith('4.') -and $snapshot.snapshot.pointerSize -eq '8' -and $snapshot.snapshot.executable -ieq $FullMsbuild) 'Razor did not execute in reviewed CLR4 amd64 MSBuild'
    $assemblies = @($snapshot.snapshot.assembly)
    Assert-Condition (@($assemblies | Where-Object { $_.name -eq 'RazorGenerator.MsBuild' }).Count -eq 1 -and @($assemblies | Where-Object { $_.name -match '^RazorGenerator.Core.v[123]$' }).Count -eq 1) 'Actual Razor task / engine load evidence missing or ambiguous'
    foreach ($assembly in $assemblies) {
        $path = [IO.Path]::GetFullPath($assembly.path)
        Assert-Condition ($path.StartsWith($PackageDirectory + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) 'Razor assembly loaded outside sealed package'
        $relative = [IO.Path]::GetRelativePath($PackageDirectory, $path).Replace('\', '/')
        $matches = @($script:Inputs.payload | Where-Object { $_.path -ceq $relative })
        Assert-Condition ($matches.Count -eq 1 -and $assembly.sha256 -ceq $matches[0].sha256 -and (Get-Digest $path) -ceq $matches[0].sha256) 'Loaded Razor assembly hash differs'
    }
}
function Invoke-RazorGeneration([string]$Repository, [string]$Variant, [string]$Evidence, [string]$FullMsbuild) {
    Invoke-Tool @('prepare', $Repository, $Variant, $Evidence) "prepare-$Variant"
    if ($Variant -ne 'checked-in') {
        $package = Join-Path $Repository 'packages/RazorGenerator.MsBuild.2.5.0'
        Expand-ReviewedZip (Join-Path $script:Feed 'razorgenerator.msbuild.2.5.0.nupkg') $package
        foreach ($item in $script:Inputs.payload) { Assert-Condition ((Get-Digest (Join-Path $package $item.path)) -ceq $item.sha256) 'Razor package payload changed' }
        $core = Join-Path $Repository 'src/Hangfire.Core'
        $taskSource = [Security.SecurityElement]::Escape((Join-Path $PSScriptRoot 'CaptureRazorAssemblies.cs'))
        $snapshot = [Security.SecurityElement]::Escape((Join-Path $Evidence 'razor-assemblies.xml'))
        $wrapper = @'
<Project>
<Import Project="Razor.build"/>
<UsingTask TaskName="CaptureRazorAssemblies" TaskFactory="RoslynCodeTaskFactory" AssemblyFile="$(MSBuildToolsPath)\Microsoft.Build.Tasks.Core.dll">
<Task><Reference Include="System.Xml.Linq"/><Code Type="Class" Language="cs" Source="__TASK_SOURCE__"/></Task>
</UsingTask>
<Target Name="CaptureReviewedRazorAssemblies" AfterTargets="PrecompileRazorFiles"><CaptureRazorAssemblies OutputPath="__SNAPSHOT__"/></Target>
</Project>
'@
        $file = Join-Path $core 'Razor.Verification.proj'
        [IO.File]::WriteAllText($file, $wrapper.Replace('__TASK_SOURCE__', $taskSource).Replace('__SNAPSHOT__', $snapshot), [Text.UTF8Encoding]::new($false))
        Invoke-OwnedCommand $FullMsbuild @($file, '/t:PrecompileRazorFiles', '/nologo', '/m:1', '/nr:false', '/v:diagnostic', "/bl:$Evidence/razor.binlog;ProjectImports=None") "generate-$Variant" -Directory $core -TimeoutSeconds 300 | Out-Null
        Assert-RazorSnapshot (Join-Path $Evidence 'razor-assemblies.xml') $package $FullMsbuild
        Copy-Item -LiteralPath $file -Destination (Join-Path $Evidence 'Razor.Verification.proj.xml')
    }
    Invoke-Tool @('generated', $Repository, $Variant, $Evidence) "generated-$Variant"
    $generated = Join-Path $Evidence 'generated'
    [IO.Directory]::CreateDirectory($generated) | Out-Null
    foreach ($pair in $script:Inputs.pairs) { Copy-Item -LiteralPath (Join-Path $Repository $pair.output) -Destination (Join-Path $generated ([IO.Path]::GetFileName($pair.output))) }
}
