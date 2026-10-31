function Get-WindowsInventory {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
    Assert-Condition (Test-Path -LiteralPath $vswhere) 'vswhere missing'
    $text = Invoke-OwnedCommand $vswhere @('-products', '*', '-version', '[17.0,18.0)', '-requires', 'Microsoft.Component.MSBuild', '-format', 'json', '-utf8') 'vswhere'
    $instances = @($text | ConvertFrom-Json -AsHashtable)
    Assert-Condition ($instances.Count -eq 1) 'Require exactly one eligible VS 2022 instance'
    $vs = $instances[0]
    $msbuild = Join-Path $vs.installationPath 'MSBuild/Current/Bin/amd64/MSBuild.exe'
    Assert-Condition (Test-Path -LiteralPath $msbuild) 'Full amd64 MSBuild missing'
    $versionOutput = Invoke-OwnedCommand $msbuild @('/nologo', '/version') 'msbuild-version'
    $versions = @($versionOutput -split '\r?\n' | Where-Object { $_ -match '^\d+\.\d+\.\d+(\.\d+)?\s*$' })
    Assert-Condition ($versions.Count -eq 1) 'Cannot identify exact Full MSBuild version'
    $framework = Join-Path $env:windir 'Microsoft.NET/Framework64/v4.0.30319'
    $compatibility = @('Microsoft.Build.Utilities.v4.0.dll', 'Microsoft.Build.Framework.dll', 'System.Web.dll', 'System.ComponentModel.Composition.dll') | ForEach-Object {
        $file = Join-Path $framework $_
        Assert-Condition (Test-Path -LiteralPath $file) "CLR4 compatibility assembly missing: $_"
        @{ path = $file; name = [Reflection.AssemblyName]::GetAssemblyName($file).FullName; sha256 = Get-Digest $file }
    }
    $release = (Get-ItemProperty -LiteralPath 'HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full').Release
    Assert-Condition ($release -ge 528040) 'Require installed .NET Framework 4.8 or newer'
    $inventory = @{
        runId = $env:GITHUB_RUN_ID; attempt = $env:GITHUB_RUN_ATTEMPT; commit = $env:GITHUB_SHA
        imageOS = $env:ImageOS; imageVersion = $env:ImageVersion; os = [Environment]::OSVersion.VersionString
        powershell = $PSVersionTable.PSVersion.ToString(); vswhere = @{ path = $vswhere; sha256 = Get-Digest $vswhere }
        visualStudio = @{ path = $vs.installationPath; version = $vs.installationVersion }
        msbuild = @{ path = $msbuild; version = $versions[0].Trim(); fileVersion = (Get-Item -LiteralPath $msbuild).VersionInfo.FileVersion; sha256 = Get-Digest $msbuild }
        frameworkRelease = $release; compatibility = @($compatibility); upstreamTasksExecuted = $false
    }
    Write-Json (Join-Path $script:Evidence 'inventory.json') $inventory
    return $inventory
}
function Assert-ReviewedInventory($Inventory, [string]$Image, [string]$Vs, [string]$Msbuild, [string]$MsbuildSha256) {
    Assert-Condition (-not [string]::IsNullOrWhiteSpace($Image) -and -not [string]::IsNullOrWhiteSpace($Vs) -and -not [string]::IsNullOrWhiteSpace($Msbuild) -and $MsbuildSha256 -cmatch '^[a-f0-9]{64}$') 'All four reviewed inventory values required'
    Assert-Condition ($Inventory.imageVersion -ceq $Image -and $Inventory.visualStudio.version -ceq $Vs -and $Inventory.msbuild.version -ceq $Msbuild -and $Inventory.msbuild.sha256 -ceq $MsbuildSha256) 'Unreviewed image / VS / Full MSBuild drift'
}
