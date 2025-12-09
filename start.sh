#!/usr/bin/env bash
set -euo pipefail

CONFIGURATION="${CONFIGURATION:-Debug}"
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
API_PROJECT="$ROOT_DIR/Radish.Api/Radish.Api.csproj"
AUTH_PROJECT="$ROOT_DIR/Radish.Auth/Radish.Auth.csproj"
CLIENT_DIR="$ROOT_DIR/radish.client"
CONSOLE_DIR="$ROOT_DIR/radish.console"
DBMIGRATE_PROJECT="$ROOT_DIR/Radish.DbMigrate/Radish.DbMigrate.csproj"
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

# 记录后台服务 PID，脚本退出或中断时统一清理
BG_PIDS=()

add_bg_pid() {
  BG_PIDS+=("$1")
}

cleanup() {
  if ((${#BG_PIDS[@]} == 0)); then
    return
  fi
  echo
  echo "==> 正在停止后台服务..."
  for pid in "${BG_PIDS[@]}"; do
    if kill -0 "$pid" 2>/dev/null; then
      echo "  - kill $pid"
      kill "$pid" 2>/dev/null || true
    fi
  done
}

trap cleanup EXIT INT TERM

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
  echo "  0. 退出"
  echo
  echo "[单服务]"
  echo "  1. 启动 API           (Radish.Api           @ http://localhost:5100)"
  echo "  2. 启动 Gateway       (Radish.Gateway       @ https://localhost:5000)"
  echo "  3. 启动 Frontend      (radish.client        @ http://localhost:3000)"
  echo "  4. 启动 Docs          (radish.docs          @ http://localhost:3001/docs/)"
  echo "  5. 启动 Console       (radish.console       @ http://localhost:3002)"
  echo "  6. 启动 Auth          (Radish.Auth          @ http://localhost:5200)"
  echo "  7. 运行 DbMigrate     (Radish.DbMigrate     @ init/seed)"
  echo "  8. 运行单元测试       (Radish.Api.Tests)"
  echo
  echo "[组合启动]"
  echo "  9. 启动 Gateway + API"
  echo " 10. 启动 Gateway + Frontend"
  echo " 11. 启动 Gateway + Docs"
  echo " 12. 启动 Gateway + Console"
  echo " 13. 启动 Gateway + Auth"
  echo " 14. 启动 Gateway + Auth + API"
  echo " 15. 启动 Gateway + API + Frontend"
  echo " 16. 启动 Gateway + API + Frontend + Console"
  echo " 17. 一键启动全部服务 (Gateway + API + Auth + Frontend + Docs + Console)"
  echo
  echo "提示: 组合启动会将 Gateway / 前端 / 文档 / Console / Auth 置于后台, API 通常在前台运行以便查看日志。"
  echo
}

# ---- 基础启动函数 ----

build_all() {
  (
    cd "$ROOT_DIR"
    invoke_step "dotnet build Radish.slnx ($CONFIGURATION)" dotnet build Radish.slnx -c "$CONFIGURATION"
  )
}

start_api() {
  (
    cd "$ROOT_DIR"
    export ASPNETCORE_URLS="http://localhost:5100"

    invoke_step "dotnet clean ($CONFIGURATION)" dotnet clean "$API_PROJECT" -c "$CONFIGURATION"
    invoke_step "dotnet restore" dotnet restore "$API_PROJECT"
    invoke_step "dotnet build ($CONFIGURATION)" dotnet build "$API_PROJECT" -c "$CONFIGURATION"
    invoke_step "dotnet run (http only)" dotnet run --project "$API_PROJECT" -c "$CONFIGURATION" --launch-profile http
  )
}

start_api_no_build() {
  (
    cd "$ROOT_DIR"
    export ASPNETCORE_URLS="http://localhost:5100"
    invoke_step "dotnet run API (no-build, http only)" dotnet run --no-build --project "$API_PROJECT" -c "$CONFIGURATION" --launch-profile http
  )
}

start_gateway() {
  (
    cd "$ROOT_DIR"
    dotnet run --project Radish.Gateway/Radish.Gateway.csproj --launch-profile https
  )
}

start_gateway_no_build() {
  (
    cd "$ROOT_DIR"
    exec dotnet run --no-build --project Radish.Gateway/Radish.Gateway.csproj --launch-profile https
  )
}

start_frontend() {
  (
    cd "$ROOT_DIR"
    exec npm run dev --prefix "$CLIENT_DIR"
  )
}

start_docs() {
  (
    cd "$ROOT_DIR"
    exec npm run docs:dev --prefix radish.docs
  )
}

start_console() {
  (
    cd "$ROOT_DIR"
    exec npm run dev --prefix "$CONSOLE_DIR"
  )
}

start_auth() {
  (
    cd "$ROOT_DIR"
    export ASPNETCORE_URLS="http://localhost:5200"

    invoke_step "dotnet clean ($CONFIGURATION)" dotnet clean "$AUTH_PROJECT" -c "$CONFIGURATION"
    invoke_step "dotnet restore" dotnet restore "$AUTH_PROJECT"
    invoke_step "dotnet build ($CONFIGURATION)" dotnet build "$AUTH_PROJECT" -c "$CONFIGURATION"
    invoke_step "dotnet run (http only)" dotnet run --project "$AUTH_PROJECT" -c "$CONFIGURATION" --launch-profile http
  )
}

start_auth_no_build() {
  (
    cd "$ROOT_DIR"
    export ASPNETCORE_URLS="http://localhost:5200"
    exec dotnet run --no-build --project "$AUTH_PROJECT" -c "$CONFIGURATION" --launch-profile http
  )
}

start_dbmigrate() {
  (
    cd "$ROOT_DIR"
    echo ""
    echo "DbMigrate 命令说明："
    echo "  init - 仅初始化数据库表结构（Code First）"
    echo "  seed - 填充初始数据（如表不存在会自动执行 init）"
    echo ""
    read -rp "请选择 DbMigrate 子命令 [init/seed] (默认 seed, 输入 q 取消): " migrate_cmd
    if [[ -z "${migrate_cmd:-}" ]]; then
      migrate_cmd="seed"
    fi
    if [[ "$migrate_cmd" == "q" ]]; then
      echo "已取消执行 DbMigrate。"
      return
    fi
    case "$migrate_cmd" in
      init | seed)
        invoke_step "dotnet run DbMigrate ($migrate_cmd, $CONFIGURATION)" dotnet run --project "$DBMIGRATE_PROJECT" -c "$CONFIGURATION" -- "$migrate_cmd"
        ;;
      *)
        echo "未知 DbMigrate 子命令: $migrate_cmd" >&2
        exit 1
        ;;
    esac
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
  build_all
  start_gateway_no_build &
  add_bg_pid $!
  echo "  - Gateway 已在后台启动 (https://localhost:5000)."
  start_api_no_build
}

start_gateway_frontend() {
  echo "[组合] 启动 Gateway + Frontend..."
  build_all
  start_gateway_no_build &
  add_bg_pid $!
  echo "  - Gateway 已在后台启动 (https://localhost:5000)."
  start_frontend
}

start_gateway_docs() {
  echo "[组合] 启动 Gateway + Docs..."
  build_all
  start_gateway_no_build &
  add_bg_pid $!
  echo "  - Gateway 已在后台启动 (https://localhost:5000)."
  start_docs
}

start_gateway_console() {
  echo "[组合] 启动 Gateway + Console..."
  build_all
  start_gateway_no_build &
  add_bg_pid $!
  echo "  - Gateway 已在后台启动 (https://localhost:5000)."
  start_console
}

start_gateway_auth() {
  echo "[组合] 启动 Gateway + Auth..."
  build_all
  start_gateway_no_build &
  add_bg_pid $!
  echo "  - Gateway 已在后台启动 (https://localhost:5000)."
  start_auth_no_build
}

start_gateway_auth_api() {
  echo "[组合] 启动 Gateway + Auth + API..."
  build_all
  start_gateway_no_build &
  add_bg_pid $!
  echo "  - Gateway 已在后台启动 (https://localhost:5000)."
  start_auth_no_build &
  add_bg_pid $!
  echo "  - Auth 已在后台启动 (http://localhost:5200)."
  start_api_no_build
}

start_gateway_api_frontend() {
  echo "[组合] 启动 Gateway + API + Frontend..."
  build_all
  start_gateway_no_build &
  add_bg_pid $!
  echo "  - Gateway 已在后台启动 (https://localhost:5000)."
  start_frontend &
  add_bg_pid $!
  echo "  - Frontend 已在后台启动 (https://localhost:3000)."
  start_api_no_build
}

start_gateway_api_frontend_console() {
  echo "[组合] 启动 Gateway + API + Frontend + Console..."
  build_all
  start_gateway_no_build &
  add_bg_pid $!
  echo "  - Gateway 已在后台启动 (https://localhost:5000)."
  start_frontend &
  add_bg_pid $!
  echo "  - Frontend 已在后台启动 (https://localhost:3000)."
  start_console &
  add_bg_pid $!
  echo "  - Console 已在后台启动 (https://localhost:3002)."
  start_api_no_build
}

start_all() {
  echo "[组合] 一键启动全部服务 (Gateway + API + Auth + Frontend + Docs + Console)..."
  build_all
  start_gateway_no_build &
  add_bg_pid $!
  echo "  - Gateway 已在后台启动 (https://localhost:5000)."
  start_auth_no_build &
  add_bg_pid $!
  echo "  - Auth 已在后台启动 (http://localhost:5200)."
  start_frontend &
  add_bg_pid $!
  echo "  - Frontend 已在后台启动 (https://localhost:3000)."
  start_docs &
  add_bg_pid $!
  echo "  - Docs 已在后台启动 (http://localhost:3001/docs/)."
  start_console &
  add_bg_pid $!
  echo "  - Console 已在后台启动 (https://localhost:3002)."
  start_api_no_build
}

# ---- 主逻辑 ----

print_banner
print_menu
read -rp "请输入选项编号: " choice

case "$choice" in
  0)
    echo "已退出。"
    exit 0
    ;;
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
    start_auth
    ;;
  7)
    start_dbmigrate
    ;;
  8)
    run_tests
    ;;
  9)
    start_gateway_api
    ;;
  10)
    start_gateway_frontend
    ;;
  11)
    start_gateway_docs
    ;;
  12)
    start_gateway_console
    ;;
  13)
    start_gateway_auth
    ;;
  14)
    start_gateway_auth_api
    ;;
  15)
    start_gateway_api_frontend
    ;;
  16)
    start_gateway_api_frontend_console
    ;;
  17)
    start_all
    ;;
  *)
    echo "Unknown option: $choice" >&2
    exit 1
    ;;
esac
