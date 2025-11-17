param(
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = 'Stop'
$projectPath = Join-Path $PSScriptRoot "Radish.Api/Radish.Api.csproj"

if (-not (Test-Path $projectPath)) {
    Write-Error "Radish.Api.csproj not found. Run this script from the repository root."
    exit 1
}

function Invoke-Step {
    param(
        [string]$Message,
        [ScriptBlock]$Action
    )

    Write-Host "==> $Message"
    & $Action
}

Invoke-Step "dotnet clean ($Configuration)" {
    dotnet clean $projectPath -c $Configuration
}

Invoke-Step "dotnet restore" {
    dotnet restore $projectPath
}

Invoke-Step "dotnet build ($Configuration)" {
    dotnet build $projectPath -c $Configuration
}

Invoke-Step "dotnet run ($Configuration)" {
    dotnet run --project $projectPath -c $Configuration
}
