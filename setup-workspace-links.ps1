# 为 npm workspaces 子项目创建符号链接
# 使得可以在子项目目录中直接运行 npm 命令

Write-Host "Setting up workspace symlinks..." -ForegroundColor Green

$workspaces = @("radish.client", "radish.console", "radish.ui")

foreach ($workspace in $workspaces) {
    $targetPath = Join-Path $PSScriptRoot "node_modules\.bin"
    $linkPath = Join-Path $PSScriptRoot "$workspace\node_modules\.bin"

    # 确保子项目的 node_modules 目录存在
    $nodeModulesDir = Join-Path $PSScriptRoot "$workspace\node_modules"
    if (-not (Test-Path $nodeModulesDir)) {
        New-Item -ItemType Directory -Path $nodeModulesDir -Force | Out-Null
    }

    # 删除旧的符号链接（如果存在）
    if (Test-Path $linkPath) {
        Remove-Item $linkPath -Force -Recurse
    }

    # 创建符号链接
    try {
        New-Item -ItemType SymbolicLink -Path $linkPath -Target $targetPath -Force | Out-Null
        Write-Host "✓ Created symlink for $workspace" -ForegroundColor Green
    } catch {
        Write-Host "✗ Failed to create symlink for $workspace" -ForegroundColor Red
        Write-Host "  Error: $_" -ForegroundColor Red
        Write-Host "  You may need to run PowerShell as Administrator" -ForegroundColor Yellow
    }
}

Write-Host "`nSetup complete!" -ForegroundColor Green
Write-Host "You can now run 'npm run dev' directly in workspace directories." -ForegroundColor Cyan
