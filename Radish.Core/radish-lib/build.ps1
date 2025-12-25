# Build script for radish-lib (Windows PowerShell)

$ErrorActionPreference = "Stop"

Write-Host "=========================================" -ForegroundColor Green
Write-Host "Building radish-lib (Rust Native Extensions)" -ForegroundColor Green
Write-Host "=========================================" -ForegroundColor Green

# Get the script directory
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $ScriptDir

# Check if Rust is installed
if (-not (Get-Command cargo -ErrorAction SilentlyContinue)) {
    Write-Host "Error: Rust is not installed. Please install Rust first:" -ForegroundColor Red
    Write-Host "  https://rustup.rs/" -ForegroundColor Yellow
    exit 1
}

# Check if font file exists
if (-not (Test-Path "fonts\DejaVuSans.ttf")) {
    Write-Host "Warning: Font file not found at fonts\DejaVuSans.ttf" -ForegroundColor Yellow
    Write-Host "Watermark functionality may not work properly." -ForegroundColor Yellow
    Write-Host "See fonts\README.md for instructions." -ForegroundColor Yellow
}

# Build in release mode
Write-Host ""
Write-Host "Building in release mode..." -ForegroundColor Cyan
cargo build --release

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "✓ Build successful!" -ForegroundColor Green
    Write-Host ""

    $LibName = "radish_lib.dll"

    # Copy to Radish.Api output directory
    $ApiOutputDir = "..\..\..\Radish.Api\bin\Debug\net10.0"

    if (Test-Path $ApiOutputDir) {
        Write-Host "Copying $LibName to $ApiOutputDir..." -ForegroundColor Cyan
        Copy-Item "target\release\$LibName" "$ApiOutputDir\" -Force
        Write-Host "✓ Library copied successfully!" -ForegroundColor Green
    } else {
        Write-Host "Warning: API output directory not found: $ApiOutputDir" -ForegroundColor Yellow
        Write-Host "You may need to build Radish.Api first or copy the library manually." -ForegroundColor Yellow
    }

    Write-Host ""
    Write-Host "Library location: target\release\$LibName" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "=========================================" -ForegroundColor Green
    Write-Host "Build complete!" -ForegroundColor Green
    Write-Host "=========================================" -ForegroundColor Green
} else {
    Write-Host ""
    Write-Host "✗ Build failed!" -ForegroundColor Red
    exit 1
}
