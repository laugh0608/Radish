param(
  [string]$Domains = "localhost 127.0.0.1 ::1"
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$certDir = Join-Path $root 'dev-certs'
Write-Host "[ssl-setup] 目标目录: $certDir"
New-Item -ItemType Directory -Force -Path $certDir | Out-Null

if (-not (Get-Command mkcert -ErrorAction SilentlyContinue)) {
  Write-Host "[ssl-setup] 未检测到 mkcert，请先安装："
  Write-Host "  Windows: choco install mkcert  或  scoop install mkcert"
  Write-Host "  其他平台: https://github.com/FiloSottile/mkcert"
  exit 1
}

Write-Host "[ssl-setup] 安装本地根证书（若已安装会自动跳过）"
mkcert -install | Out-Null

$key = Join-Path $certDir 'localhost.key'
$crt = Join-Path $certDir 'localhost.crt'

Write-Host "[ssl-setup] 生成 localhost 证书"
mkcert -key-file $key -cert-file $crt $Domains

Write-Host "[ssl-setup] 完成。React/Angular 将自动使用 dev-certs 下的证书。"

