param(
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = 'Stop'
$repoRoot = $PSScriptRoot
$projectPath = Join-Path $repoRoot "Radish.Api/Radish.Api.csproj"
$clientPath = Join-Path $repoRoot "radish.client"
$testProjectPath = Join-Path $repoRoot "Radish.Api.Tests/Radish.Api.Tests.csproj"

if (-not (Test-Path $projectPath)) {
    Write-Error "Radish.Api.csproj not found. Run this script from the repository root."
    exit 1
}

function Ensure-Command {
    param([string]$Name)

    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "未找到命令 '$Name'，请先安装或将其添加到 PATH。"
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

function Start-Backend {
    Push-Location $repoRoot
    try {
        Write-Host "准备以配置 $Configuration 启动后端 (Radish.Api)。"
        $env:ASPNETCORE_URLS = "https://localhost:7110;http://localhost:5165"

        Invoke-Step "dotnet clean ($Configuration)" {
            dotnet clean $projectPath -c $Configuration
        }

        Invoke-Step "dotnet restore" {
            dotnet restore $projectPath
        }

        Invoke-Step "dotnet build ($Configuration)" {
            dotnet build $projectPath -c $Configuration
        }

        Invoke-Step "dotnet run (https + http)" {
            dotnet run --project $projectPath -c $Configuration --launch-profile https
        }
    }
    finally {
        Pop-Location
    }
}

function Start-Frontend {
    param(
        [switch]$Detached
    )

    $command = "Set-Location `"$repoRoot`"; npm run dev --prefix radish.client"
    if ($Detached) {
        $shellExe = if ($PSVersionTable.PSEdition -eq 'Core') { "pwsh" } else { "powershell" }
        Start-Process -FilePath $shellExe -ArgumentList "-NoExit", "-Command", $command | Out-Null
        Write-Host "前端已在新的终端窗口中启动（npm run dev --prefix radish.client）。"
        return
    }

    Push-Location $repoRoot
    try {
        npm run dev --prefix radish.client
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

Write-Host "请选择要启动的服务："
Write-Host "1. 启动前端 (radish.client)"
Write-Host "2. 启动后端 (Radish.Api)"
Write-Host "3. 前后端一起启动"
Write-Host "4. 执行单元测试 (Radish.Api.Tests)"
$choice = Read-Host "输入选项 (1/2/3/4)"

switch ($choice) {
    "1" {
        Start-Frontend
    }
    "2" {
        Start-Backend
    }
    "3" {
        Start-Frontend -Detached
        Start-Backend
    }
    "4" {
        Run-Tests
    }
    default {
        Write-Error "未知选项：$choice"
        exit 1
    }
}
