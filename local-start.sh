#!/usr/bin/env bash
set -euo pipefail

CONFIGURATION="${CONFIGURATION:-Debug}"
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
API_PROJECT="$ROOT_DIR/Radish.Api/Radish.Api.csproj"
CLIENT_DIR="$ROOT_DIR/radish.client"
TEST_PROJECT="$ROOT_DIR/Radish.Api.Tests/Radish.Api.Tests.csproj"

if [[ ! -f "$API_PROJECT" ]]; then
  echo "未找到 $API_PROJECT，请在仓库根目录运行该脚本。" >&2
  exit 1
fi

ensure_command() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "未找到命令 '$1'，请先安装后再运行脚本。" >&2
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

start_frontend() {
  (cd "$ROOT_DIR" && npm run dev --prefix "$CLIENT_DIR")
}

start_backend() {
  (
    cd "$ROOT_DIR"
    export ASPNETCORE_URLS="https://localhost:7110;http://localhost:5165"

    invoke_step "dotnet clean ($CONFIGURATION)" dotnet clean "$API_PROJECT" -c "$CONFIGURATION"
    invoke_step "dotnet restore" dotnet restore "$API_PROJECT"
    invoke_step "dotnet build ($CONFIGURATION)" dotnet build "$API_PROJECT" -c "$CONFIGURATION"
    invoke_step "dotnet run (https + http)" dotnet run --project "$API_PROJECT" -c "$CONFIGURATION" --launch-profile https
  )
}

run_tests() {
  (
    cd "$ROOT_DIR"
    invoke_step "dotnet test (Radish.Api.Tests)" dotnet test "$TEST_PROJECT" -c "$CONFIGURATION"
  )
}

cleanup() {
  if [[ -n "${FRONTEND_PID:-}" ]]; then
    if ps -p "$FRONTEND_PID" >/dev/null 2>&1; then
      kill "$FRONTEND_PID" >/dev/null 2>&1 || true
    fi
  fi
}

echo "请选择要启动的服务："
echo "1. 启动前端 (radish.client)"
echo "2. 启动后端 (Radish.Api)"
echo "3. 前后端一起启动"
echo "4. 执行单元测试 (Radish.Api.Tests)"
read -rp "输入选项 (1/2/3/4): " choice

case "$choice" in
  1)
    start_frontend
    ;;
  2)
    start_backend
    ;;
  3)
    start_frontend &
    FRONTEND_PID=$!
    trap cleanup EXIT
    echo "前端已在后台启动 (PID: $FRONTEND_PID)，按 Ctrl+C 可在终端中停止后端。"
    start_backend
    ;;
  4)
    run_tests
    ;;
  *)
    echo "未知选项: $choice" >&2
    exit 1
    ;;
esac
