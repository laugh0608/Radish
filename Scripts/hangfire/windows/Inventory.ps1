function New-WindowsInventory {
    $inventory = @{
        schemaVersion = 3; complete = $false; stage = 'powershell-contracts'
        runId = $env:GITHUB_RUN_ID; attempt = $env:GITHUB_RUN_ATTEMPT; commit = $env:GITHUB_SHA
        workflowSha = $env:GITHUB_WORKFLOW_SHA; workflowRef = $env:GITHUB_WORKFLOW_REF
        runnerArchitecture = $env:RUNNER_ARCH
        processArchitecture = [Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString()
        osArchitecture = [Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString()
        imageOS = $env:ImageOS; imageVersion = $env:ImageVersion; os = [Environment]::OSVersion.VersionString
        powershell = $PSVersionTable.PSVersion.ToString()
        vswhere = $null; visualStudio = $null; msbuild = $null; frameworkRelease = $null
        compatibility = @(); upstreamTasksExecuted = $false; imageIdentity = $null
    }
    Write-Json (Join-Path $script:Evidence 'inventory-progress.json') $inventory
    $inventory.imageIdentity = Get-RunnerImageIdentity
    Write-Json (Join-Path $script:Evidence 'inventory-progress.json') $inventory
    return $inventory
}

function Read-VisualStudioInventory([hashtable]$Inventory, [string]$Json, [string]$ProgressPath) {
    Assert-OwnedPath $ProgressPath | Out-Null
    $Inventory.stage = 'visual-studio-selection'
    $Inventory.vswhere.rawOutput = $Json
    # Persist the exact response before parsing or rejecting zero / multiple candidates.
    Write-Json $ProgressPath $Inventory
    $instances = @($Json | ConvertFrom-Json -AsHashtable)
    $Inventory.vswhere.instances = $instances
    Write-Json $ProgressPath $Inventory
    Assert-Condition ($instances.Count -eq 1) 'Require exactly one eligible VS 2022 instance'
    $vs = $instances[0]
    $version = [version]$vs.installationVersion
    Assert-Condition ($version -ge [version]'17.0' -and $version -lt [version]'18.0') 'Selected instance is not VS 2022'
    $Inventory.visualStudio = @{ path = $vs.installationPath; version = $vs.installationVersion }
    Write-Json $ProgressPath $Inventory
    return $vs
}

function Get-WindowsInventory([hashtable]$Inventory) {
    $progress = Join-Path $script:Evidence 'inventory-progress.json'
    try {
        $Inventory.stage = 'vswhere-identity'
        $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
        $query = @('-products', '*', '-version', '[17.0,18.0)', '-requires', 'Microsoft.Component.MSBuild', '-format', 'json', '-utf8')
        $Inventory.vswhere = @{ path = $vswhere; sha256 = $null; arguments = $query; rawOutput = $null; instances = $null }
        Write-Json $progress $Inventory
        Assert-Condition (Test-Path -LiteralPath $vswhere) 'vswhere missing'
        $Inventory.vswhere.sha256 = Get-Digest $vswhere
        $Inventory.stage = 'vswhere-query'
        Write-Json $progress $Inventory
        $text = Invoke-OwnedCommand $vswhere $query 'vswhere'
        $vs = Read-VisualStudioInventory $Inventory $text $progress

        $Inventory.stage = 'msbuild-identity'
        $msbuild = Join-Path $vs.installationPath 'MSBuild/Current/Bin/amd64/MSBuild.exe'
        $Inventory.msbuild = @{ path = $msbuild; version = $null; fileVersion = $null; sha256 = $null }
        Write-Json $progress $Inventory
        Assert-Condition (Test-Path -LiteralPath $msbuild) 'Full amd64 MSBuild missing'
        $Inventory.msbuild.fileVersion = (Get-Item -LiteralPath $msbuild).VersionInfo.FileVersion
        $Inventory.msbuild.sha256 = Get-Digest $msbuild
        $Inventory.stage = 'msbuild-version'
        Write-Json $progress $Inventory
        $versionOutput = Invoke-OwnedCommand $msbuild @('/nologo', '/version') 'msbuild-version'
        $versions = @($versionOutput -split '\r?\n' | Where-Object { $_ -match '^\d+\.\d+\.\d+(\.\d+)?\s*$' })
        Assert-Condition ($versions.Count -eq 1) 'Cannot identify exact Full MSBuild version'
        $Inventory.msbuild.version = $versions[0].Trim()

        $Inventory.stage = 'framework-compatibility'
        Write-Json $progress $Inventory
        $framework = Join-Path $env:windir 'Microsoft.NET/Framework64/v4.0.30319'
        foreach ($assemblyName in @('Microsoft.Build.Utilities.v4.0.dll', 'Microsoft.Build.Framework.dll', 'System.Web.dll', 'System.ComponentModel.Composition.dll')) {
            $file = Join-Path $framework $assemblyName
            Assert-Condition (Test-Path -LiteralPath $file) "CLR4 compatibility assembly missing: $assemblyName"
            $Inventory.compatibility += @{ path = $file; name = [Reflection.AssemblyName]::GetAssemblyName($file).FullName; sha256 = Get-Digest $file }
            Write-Json $progress $Inventory
        }
        $Inventory.stage = 'framework-release'
        $Inventory.frameworkRelease = (Get-ItemProperty -LiteralPath 'HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full').Release
        Write-Json $progress $Inventory
        Assert-Condition ($Inventory.frameworkRelease -ge 528040) 'Require installed .NET Framework 4.8 or newer'

        # Only the completed snapshot is eligible for review / verify. Progress remains explicitly incomplete.
        $Inventory.stage = 'checks-complete'
        Write-Json $progress $Inventory
        $completed = $Inventory.Clone()
        $completed.complete = $true
        $completed.stage = 'complete'
        Write-Json (Join-Path $script:Evidence 'inventory.json') $completed
        return $completed
    } catch {
        $Inventory.error = $_.Exception.Message
        Write-Json $progress $Inventory
        throw
    }
}

function Assert-ReviewedInventory($Inventory, [string]$Image, [string]$Vs, [string]$Msbuild, [string]$MsbuildSha256) {
    Assert-Condition ($Inventory.complete -eq $true) 'Complete inventory required for verify'
    Assert-ConsistentImageIdentity $Inventory
    Assert-Condition (-not [string]::IsNullOrWhiteSpace($Image) -and -not [string]::IsNullOrWhiteSpace($Vs) -and -not [string]::IsNullOrWhiteSpace($Msbuild) -and $MsbuildSha256 -cmatch '^[a-f0-9]{64}$') 'All four reviewed inventory values required'
    Assert-Condition ($Inventory.imageVersion -ceq $Image -and $Inventory.visualStudio.version -ceq $Vs -and $Inventory.msbuild.version -ceq $Msbuild -and $Inventory.msbuild.sha256 -ceq $MsbuildSha256) 'Unreviewed image / VS / Full MSBuild drift'
}
