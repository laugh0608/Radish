param(
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = 'Stop'
$repoRoot = $PSScriptRoot
$projectPath = Join-Path $repoRoot "Radish.Api/Radish.Api.csproj"
$authProjectPath = Join-Path $repoRoot "Radish.Auth/Radish.Auth.csproj"
$clientPath = Join-Path $repoRoot "radish.client"
$consolePath = Join-Path $repoRoot "radish.console"
$dbMigrateProjectPath = Join-Path $repoRoot "Radish.DbMigrate/Radish.DbMigrate.csproj"
$testProjectPath = Join-Path $repoRoot "Radish.Api.Tests/Radish.Api.Tests.csproj"

if (-not (Test-Path $projectPath)) {
    Write-Error "Radish.Api.csproj not found. Run this script from the repository root."
    exit 1
}

function Ensure-Command {
    param([string]$Name)

    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Command '$Name' not found. Install it or add to PATH."
    }
}

Ensure-Command dotnet
Ensure-Command npm

function Invoke-Step {
    param(
        [string]$Message,
        [ScriptBlock]$Action
    )

    Write-Host "==> $Message"
    & $Action
}

function Show-Banner {
@"
====================================
   ____           _ _     _
  |  _ \ __ _  __| (_)___| |__
  | |_) / _` |/ _` | / __| '_ \
  |  _ < (_| | (_| | \__ \ | | |
  |_| \_\__,_|\__,_|_|___/_| |_|
        Radish  --by luobo
====================================
"@
}

function Show-Menu {
    Write-Host
    Write-Host "Radish dev start menu ($Configuration)"
    Write-Host "--------------------------------------"
    Write-Host "  0. Exit"
    Write-Host
    Write-Host "[Single services]"
    Write-Host "  1. Start API           (Radish.Api           @ http://localhost:5100)"
    Write-Host "  2. Start Gateway       (Radish.Gateway       @ https://localhost:5000)"
    Write-Host "  3. Start Frontend      (radish.client        @ http://localhost:3000)"
    Write-Host "  4. Start Docs          (radish.docs          @ http://localhost:3100/docs/)"
    Write-Host "  5. Start Console       (radish.console       @ http://localhost:3200)"
    Write-Host "  6. Start Auth          (Radish.Auth          @ http://localhost:5200)"
    Write-Host "  7. Run DbMigrate       (Radish.DbMigrate     @ init/seed)"
    Write-Host "  8. Run unit tests      (Radish.Api.Tests)"
    Write-Host
    Write-Host "[Combinations]"
    Write-Host "  9. Start Gateway + Auth + API"
    Write-Host " 10. Start Frontend + Console + Docs"
    Write-Host
    Write-Host "[Start ALL]"
    Write-Host " 11. Start ALL (Gateway + API + Auth + Frontend + Docs + Console)"
    Write-Host
    Write-Host "Hint: combinations launch services in separate terminals for parallel development."
    Write-Host
}

# ---- Single-service start functions ----

function Build-All {
    Push-Location $repoRoot
    try {
        Invoke-Step "dotnet build Radish.slnx ($Configuration)" {
            dotnet build (Join-Path $repoRoot "Radish.slnx") -c $Configuration
        }
    }
    finally {
        Pop-Location
    }
}

function Start-Backend {
    Push-Location $repoRoot
    try {
        Write-Host "Starting backend (Radish.Api) with configuration $Configuration."
        $env:ASPNETCORE_URLS = "http://localhost:5100"

        Invoke-Step "dotnet clean ($Configuration)" {
            dotnet clean $projectPath -c $Configuration
        }

        Invoke-Step "dotnet restore" {
            dotnet restore $projectPath
        }

        Invoke-Step "dotnet build ($Configuration)" {
            dotnet build $projectPath -c $Configuration
        }

        Invoke-Step "dotnet run (http only)" {
            dotnet run --project $projectPath -c $Configuration --launch-profile http
        }
    }
    finally {
        Pop-Location
    }
}

function Start-BackendNoBuild {
    Push-Location $repoRoot
    try {
        Write-Host "Starting backend (Radish.Api) without build."
        $env:ASPNETCORE_URLS = "http://localhost:5100"

        Invoke-Step "dotnet run API (no-build, http only)" {
            dotnet run --no-build --project $projectPath -c $Configuration --launch-profile http
        }
    }
    finally {
        Pop-Location
    }
}

function Start-Gateway {
    Push-Location $repoRoot
    try {
        dotnet run --project (Join-Path $repoRoot "Radish.Gateway/Radish.Gateway.csproj")
    }
    finally {
        Pop-Location
    }
}

function Start-GatewayNoBuild {
    Push-Location $repoRoot
    try {
        dotnet run --no-build --project (Join-Path $repoRoot "Radish.Gateway/Radish.Gateway.csproj")
    }
    finally {
        Pop-Location
    }
}

function Start-Frontend {
    Push-Location $repoRoot
    try {
        npm run dev --prefix radish.client
    }
    finally {
        Pop-Location
    }
}

function Start-Docs {
    Push-Location $repoRoot
    try {
        npm run docs:dev --prefix radish.docs
    }
    finally {
        Pop-Location
    }
}

function Start-Console {
    Push-Location $repoRoot
    try {
        npm run dev --prefix radish.console
    }
    finally {
        Pop-Location
    }
}

function Start-Auth {
    Push-Location $repoRoot
    try {
        Write-Host "Starting Auth Server (Radish.Auth) with configuration $Configuration."
        $env:ASPNETCORE_URLS = "http://localhost:5200"

        Invoke-Step "dotnet clean ($Configuration)" {
            dotnet clean $authProjectPath -c $Configuration
        }

        Invoke-Step "dotnet restore" {
            dotnet restore $authProjectPath
        }

        Invoke-Step "dotnet build ($Configuration)" {
            dotnet build $authProjectPath -c $Configuration
        }

        Invoke-Step "dotnet run (http only)" {
            dotnet run --project $authProjectPath -c $Configuration --launch-profile http
        }
    }
    finally {
        Pop-Location
    }
}

function Start-AuthNoBuild {
    Push-Location $repoRoot
    try {
        Write-Host "Starting Auth Server (Radish.Auth) without build."
        $env:ASPNETCORE_URLS = "http://localhost:5200"

        Invoke-Step "dotnet run Auth (no-build, http only)" {
            dotnet run --no-build --project $authProjectPath -c $Configuration --launch-profile http
        }
    }
    finally {
        Pop-Location
    }
}

function Start-DbMigrate {
    Push-Location $repoRoot
    try {
        Write-Host ""
        Write-Host "DbMigrate commands:"
        Write-Host "  init - Initialize database schema only"
        Write-Host "  seed - Seed initial data (auto-runs init if needed)"
        Write-Host ""
        $cmd = Read-Host "Select DbMigrate command [init/seed] (default: seed, q to cancel)"
        if ([string]::IsNullOrWhiteSpace($cmd)) {
            $cmd = "seed"
        }
        if ($cmd -eq "q") {
            Write-Host "DbMigrate cancelled."
            return
        }

        $arg = switch ($cmd) {
            "init" { "init" }
            "seed" { "seed" }
            default {
                Write-Error ("Unknown DbMigrate command: " + $cmd)
                exit 1
            }
        }

        Invoke-Step "dotnet run DbMigrate ($cmd, $Configuration)" {
            dotnet run --project $dbMigrateProjectPath -c $Configuration -- $arg
        }
    }
    finally {
        Pop-Location
    }
}

function Run-Tests {
    Push-Location $repoRoot
    try {
        Invoke-Step "dotnet test (Radish.Api.Tests)" {
            dotnet test $testProjectPath -c $Configuration
        }
    }
    finally {
        Pop-Location
    }
}

# ---- Helper: start command in a new terminal ----

function Start-BackgroundShell {
    param(
        [string]$Description,
        [string]$CommandLine
    )

    $shellExe = if ($PSVersionTable.PSEdition -eq 'Core') { "pwsh" } else { "powershell" }
    $args = @(
        "-NoExit"
        "-Command"
        $CommandLine
    )

    Start-Process -FilePath $shellExe -WorkingDirectory $repoRoot -ArgumentList $args | Out-Null
    Write-Host "  - $Description"
}

# ---- Combination start functions ----

function Start-GatewayAuthApi {
    Write-Host "[Combo] Gateway + Auth + API..."
    Build-All
    Start-BackgroundShell "Gateway running at https://localhost:5000" "dotnet run --no-build --project Radish.Gateway/Radish.Gateway.csproj --launch-profile https"
    Start-BackgroundShell "Auth running at http://localhost:5200" "dotnet run --no-build --project Radish.Auth/Radish.Auth.csproj --launch-profile http"
    Start-BackendNoBuild
}

function Start-All {
    Write-Host "[Combo] ALL: Gateway + API + Auth + Frontend + Docs + Console..."
    Build-All
    Start-BackgroundShell "Gateway running at https://localhost:5000" "dotnet run --no-build --project Radish.Gateway/Radish.Gateway.csproj --launch-profile https"
    Start-BackgroundShell "Auth running at http://localhost:5200" "dotnet run --no-build --project Radish.Auth/Radish.Auth.csproj --launch-profile http"
    Start-BackgroundShell "Frontend running at http://localhost:3000" "npm run dev --prefix radish.client"
    Start-BackgroundShell "Docs running at http://localhost:3100/docs/" "npm run docs:dev --prefix radish.docs"
    Start-BackgroundShell "Console running at http://localhost:3200" "npm run dev --prefix radish.console"
    Start-BackendNoBuild
}

function Start-FrontendConsoleDocs {
    Write-Host "[Combo] Frontend + Console + Docs..."
    Start-BackgroundShell "Frontend running at http://localhost:3000" "npm run dev --prefix radish.client"
    Start-BackgroundShell "Console running at http://localhost:3200" "npm run dev --prefix radish.console"
    Start-Docs
}

# ---- Main ----

Show-Banner
Show-Menu
$choice = Read-Host "Enter choice number"

switch ($choice) {
    "0"  { Write-Host "Bye."; exit 0 }
    "1"  { Start-Backend }
    "2"  { Start-Gateway }
    "3"  { Start-Frontend }
    "4"  { Start-Docs }
    "5"  { Start-Console }
    "6"  { Start-Auth }
    "7"  { Start-DbMigrate }
    "8"  { Run-Tests }
    "9"  { Start-GatewayAuthApi }
    "10" { Start-FrontendConsoleDocs }
    "11" { Start-All }
    default {
        Write-Error ('Unknown choice: ' + $choice)
        exit 1
    }
}
