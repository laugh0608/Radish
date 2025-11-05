$ErrorActionPreference = 'Stop'

Push-Location $PSScriptRoot

try {
    Write-Host '==> dotnet clean Radish.sln'
    dotnet clean Radish.sln

    Write-Host '==> dotnet restore'
    dotnet restore

    Write-Host '==> dotnet run --project src/Radish.HttpApi.Host'
    dotnet run --project src/Radish.HttpApi.Host
}
finally {
    Pop-Location
}
