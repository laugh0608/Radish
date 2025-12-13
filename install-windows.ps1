# Windows 环境依赖安装脚本
# 用于在 Windows PowerShell 中重新安装 npm 依赖

Write-Host "==================================" -ForegroundColor Cyan
Write-Host "  Radish Windows 环境安装脚本" -ForegroundColor Cyan
Write-Host "==================================" -ForegroundColor Cyan
Write-Host ""

# 检查是否在项目根目录
if (-not (Test-Path "package.json")) {
    Write-Host "错误：请在项目根目录运行此脚本" -ForegroundColor Red
    exit 1
}

# 检查 Node.js 是否安装
try {
    $nodeVersion = node --version
    $npmVersion = npm --version
    Write-Host "✓ Node.js 版本: $nodeVersion" -ForegroundColor Green
    Write-Host "✓ npm 版本: $npmVersion" -ForegroundColor Green
    Write-Host ""
} catch {
    Write-Host "错误：未找到 Node.js，请先安装 Node.js 24+" -ForegroundColor Red
    Write-Host "下载地址：https://nodejs.org/" -ForegroundColor Yellow
    exit 1
}

# 询问是否继续
Write-Host "此脚本将：" -ForegroundColor Yellow
Write-Host "  1. 删除所有现有的 node_modules 目录" -ForegroundColor Yellow
Write-Host "  2. 在 Windows 环境中重新安装依赖" -ForegroundColor Yellow
Write-Host ""
$confirm = Read-Host "是否继续？(y/N)"

if ($confirm -ne "y" -and $confirm -ne "Y") {
    Write-Host "已取消" -ForegroundColor Yellow
    exit 0
}

Write-Host ""
Write-Host "步骤 1/3: 清理现有的 node_modules..." -ForegroundColor Cyan

# 删除所有 node_modules
$directories = @(
    "node_modules",
    "radish.client\node_modules",
    "radish.console\node_modules",
    "radish.ui\node_modules",
    "radish.docs\node_modules"
)

foreach ($dir in $directories) {
    if (Test-Path $dir) {
        Write-Host "  删除 $dir..." -ForegroundColor Gray
        Remove-Item -Recurse -Force $dir -ErrorAction SilentlyContinue
    }
}

Write-Host "✓ 清理完成" -ForegroundColor Green
Write-Host ""

Write-Host "步骤 2/3: 安装依赖..." -ForegroundColor Cyan
Write-Host "  这可能需要几分钟..." -ForegroundColor Gray
Write-Host ""

# 安装依赖
try {
    npm install
    Write-Host ""
    Write-Host "✓ 依赖安装完成" -ForegroundColor Green
} catch {
    Write-Host ""
    Write-Host "✗ 依赖安装失败" -ForegroundColor Red
    Write-Host "错误信息：$_" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "步骤 3/3: 验证安装..." -ForegroundColor Cyan

# 验证关键依赖
$checks = @(
    @{ Path = "node_modules\vite"; Name = "Vite" },
    @{ Path = "node_modules\@radish\ui"; Name = "@radish/ui" },
    @{ Path = "node_modules\react"; Name = "React" }
)

$allOk = $true
foreach ($check in $checks) {
    if (Test-Path $check.Path) {
        Write-Host "  ✓ $($check.Name)" -ForegroundColor Green
    } else {
        Write-Host "  ✗ $($check.Name) 未找到" -ForegroundColor Red
        $allOk = $false
    }
}

Write-Host ""

if ($allOk) {
    Write-Host "==================================" -ForegroundColor Green
    Write-Host "  安装成功！" -ForegroundColor Green
    Write-Host "==================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "现在可以启动项目：" -ForegroundColor Cyan
    Write-Host "  npm run dev:frontend   # 启动前端" -ForegroundColor White
    Write-Host "  npm run dev:console    # 启动控制台" -ForegroundColor White
    Write-Host "  npm run dev:docs       # 启动文档站" -ForegroundColor White
    Write-Host ""
    Write-Host "或使用启动脚本：" -ForegroundColor Cyan
    Write-Host "  pwsh .\start.ps1" -ForegroundColor White
    Write-Host ""
} else {
    Write-Host "==================================" -ForegroundColor Red
    Write-Host "  安装可能存在问题" -ForegroundColor Red
    Write-Host "==================================" -ForegroundColor Red
    Write-Host ""
    Write-Host "请检查错误信息并重试" -ForegroundColor Yellow
    exit 1
}
