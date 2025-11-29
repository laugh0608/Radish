#!/usr/bin/env bash
set -euo pipefail

CONFIGURATION="${CONFIGURATION:-Debug}"
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
API_PROJECT="$ROOT_DIR/Radish.Api/Radish.Api.csproj"
CLIENT_DIR="$ROOT_DIR/radish.client"
CONSOLE_DIR="$ROOT_DIR/radish.console"
TEST_PROJECT="$ROOT_DIR/Radish.Api.Tests/Radish.Api.Tests.csproj"

if [[ ! -f "$API_PROJECT" ]]; then
  echo "Cannot find $API_PROJECT. Run this script from the repository root." >&2
  exit 1
fi

ensure_command() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "Command '$1' not found. Install it before running this script." >&2
    exit 1
  fi
}

ensure_command dotnet
ensure_command npm

invoke_step() {
  local message=$1
  echo "==> $message"
  shift
  "$@"
}

print_banner() {
  cat <<'EOF'
====================================
   ____           _ _     _
  |  _ \ __ _  __| (_)___| |__
  | |_) / _` |/ _` | / __| '_ \
  |  _ < (_| | (_| | \__ \ | | |
  |_| \_\__,_|\__,_|_|___/_| |_|
        Radish  --by luobo
====================================
EOF
}

print_menu() {
  echo
  echo "Radish 开发启动菜单 ($CONFIGURATION)"
  echo "------------------------------------"
  echo "[单服务]"
  echo "  1. 启动 API           (Radish.Api           @ https://localhost:7110)"
  echo "  2. 启动 Gateway       (Radish.Gateway       @ https://localhost:5001)"
  echo "  3. 启动 Frontend      (radish.client        @ https://localhost:3000)"
  echo "  4. 启动 Docs          (radish.docs          @  http://localhost:3001/docs/)"
  echo "  5. 启动 Console       (radish.console       @ https://localhost:3002)"
  echo "  6. 运行单元测试       (Radish.Api.Tests)"
  echo
  echo "[组合启动]"
  echo "  7. 启动 Gateway + API"
  echo "  8. 启动 Gateway + Frontend"
  echo "  9. 启动 Gateway + Docs"
  echo " 10. 启动 Gateway + Console"
  echo " 11. 启动 Gateway + API + Frontend"
  echo " 12. 启动 Gateway + API + Frontend + Console"
  echo " 13. 一键启动全部服务 (Gateway + API + Frontend + Docs + Console)"
  echo
  echo "提示: 组合启动会将 Gateway / 前端 / 文档 / Console 置于后台, API 通常在前台运行以便查看日志。"
  echo
}

# ---- 基础启动函数 ----

start_api() {
  (
    cd "$ROOT_DIR"
    export ASPNETCORE_URLS="https://localhost:7110;http://localhost:5165"

    invoke_step "dotnet clean ($CONFIGURATION)" dotnet clean "$API_PROJECT" -c "$CONFIGURATION"
    invoke_step "dotnet restore" dotnet restore "$API_PROJECT"
    invoke_step "dotnet build ($CONFIGURATION)" dotnet build "$API_PROJECT" -c "$CONFIGURATION"
    invoke_step "dotnet run (https + http)" dotnet run --project "$API_PROJECT" -c "$CONFIGURATION" --launch-profile https
  )
}

start_gateway() {
  (
    cd "$ROOT_DIR"
    dotnet run --project Radish.Gateway/Radish.Gateway.csproj
  )
}

start_frontend() {
  (
    cd "$ROOT_DIR"
    npm run dev --prefix "$CLIENT_DIR"
  )
}

start_docs() {
  (
    cd "$ROOT_DIR"
    npm run docs:dev --prefix radish.docs
  )
}

start_console() {
  (
    cd "$ROOT_DIR"
    npm run dev --prefix "$CONSOLE_DIR"
  )
}

run_tests() {
  (
    cd "$ROOT_DIR"
    invoke_step "dotnet test (Radish.Api.Tests)" dotnet test "$TEST_PROJECT" -c "$CONFIGURATION"
  )
}

# ---- 组合启动函数 ----

start_gateway_api() {
  echo "[组合] 启动 Gateway + API..."
  start_gateway &
  echo "  - Gateway 已在后台启动 (https://localhost:5001)."
  start_api
}

start_gateway_frontend() {
  echo "[组合] 启动 Gateway + Frontend..."
  start_gateway &
  echo "  - Gateway 已在后台启动 (https://localhost:5001)."
  start_frontend
}

start_gateway_docs() {
  echo "[组合] 启动 Gateway + Docs..."
  start_gateway &
  echo "  - Gateway 已在后台启动 (https://localhost:5001)."
  start_docs
}

start_gateway_console() {
  echo "[组合] 启动 Gateway + Console..."
  start_gateway &
  echo "  - Gateway 已在后台启动 (https://localhost:5001)."
  start_console
}

start_gateway_api_frontend() {
  echo "[组合] 启动 Gateway + API + Frontend..."
  start_gateway &
  echo "  - Gateway 已在后台启动 (https://localhost:5001)."
  start_frontend &
  echo "  - Frontend 已在后台启动 (https://localhost:3000)."
  start_api
}

start_gateway_api_frontend_console() {
  echo "[组合] 启动 Gateway + API + Frontend + Console..."
  start_gateway &
  echo "  - Gateway 已在后台启动 (https://localhost:5001)."
  start_frontend &
  echo "  - Frontend 已在后台启动 (https://localhost:3000)."
  start_console &
  echo "  - Console 已在后台启动 (https://localhost:3002)."
  start_api
}

start_all() {
  echo "[组合] 一键启动全部服务 (Gateway + API + Frontend + Docs + Console)..."
  start_gateway &
  echo "  - Gateway 已在后台启动 (https://localhost:5001)."
  start_frontend &
  echo "  - Frontend 已在后台启动 (https://localhost:3000)."
  start_docs &
  echo "  - Docs 已在后台启动 (http://localhost:3001/docs/)."
  start_console &
  echo "  - Console 已在后台启动 (https://localhost:3002)."
  start_api
}

# ---- 主逻辑 ----

print_banner
print_menu
read -rp "请输入选项编号: " choice

case "$choice" in
  1)
    start_api
    ;;
  2)
    start_gateway
    ;;
  3)
    start_frontend
    ;;
  4)
    start_docs
    ;;
  5)
    start_console
    ;;
  6)
    run_tests
    ;;
  7)
    start_gateway_api
    ;;
  8)
    start_gateway_frontend
    ;;
  9)
    start_gateway_docs
    ;;
  10)
    start_gateway_console
    ;;
  11)
    start_gateway_api_frontend
    ;;
  12)
    start_gateway_api_frontend_console
    ;;
  13)
    start_all
    ;;
  *)
    echo "Unknown option: $choice" >&2
    exit 1
    ;;
esac
