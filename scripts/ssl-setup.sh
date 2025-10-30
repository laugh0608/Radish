#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)
CERT_DIR="$ROOT_DIR/dev-certs"

echo "[ssl-setup] 目标目录: $CERT_DIR"
mkdir -p "$CERT_DIR"

if ! command -v mkcert >/dev/null 2>&1; then
  echo "[ssl-setup] 未检测到 mkcert，请先安装："
  echo "  macOS: brew install mkcert nss"
  echo "  Windows: choco install mkcert  或  scoop install mkcert"
  echo "  其他平台: https://github.com/FiloSottile/mkcert"
  exit 1
fi

echo "[ssl-setup] 安装本地根证书（若已安装会自动跳过）"
mkcert -install

KEY="$CERT_DIR/localhost.key"
CRT="$CERT_DIR/localhost.crt"

echo "[ssl-setup] 生成 localhost 证书"
mkcert -key-file "$KEY" -cert-file "$CRT" localhost 127.0.0.1 ::1

echo "[ssl-setup] 完成。React/Angular 将自动使用 dev-certs 下的证书。"

