#!/usr/bin/env bash
set -euo pipefail

CONFIGURATION="${CONFIGURATION:-Debug}"
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
API_PROJECT="$ROOT_DIR/Radish.Api/Radish.Api.csproj"
AUTH_PROJECT="$ROOT_DIR/Radish.Auth/Radish.Auth.csproj"
CLIENT_DIR="$ROOT_DIR/Frontend/radish.client"
CONSOLE_DIR="$ROOT_DIR/Frontend/radish.console"
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

invoke_step() {
  local message=$1
  echo "==> $message"
  shift
  "$@"
}

ensure_dotnet() { ensure_command dotnet; }
ensure_npm() { ensure_command npm; }

BG_NAMES=()
BG_PIDS=()
BG_PGIDS=()
RECORDED_CLEANUP_PIDS=()
CLEANUP_STARTED=0

get_process_group_id() {
  local pid=$1
  local pgid=""
  if command -v ps >/dev/null 2>&1; then
    pgid="$(ps -o pgid= -p "$pid" 2>/dev/null | tr -d '[:space:]' || true)"
  fi
  printf '%s' "$pgid"
}

enable_background_job_control() {
  if [[ $- != *m* ]]; then
    set -o monitor 2>/dev/null || true
  fi
}

add_bg_process() {
  local name=$1
  local pid=$2
  local pgid=""
  local shell_pgid=""

  pgid="$(get_process_group_id "$pid")"
  shell_pgid="$(get_process_group_id "$$")"
  if [[ -z "$pgid" || -z "$shell_pgid" || "$pgid" == "$shell_pgid" ]]; then
    pgid=""
  fi

  BG_NAMES+=("$name")
  BG_PIDS+=("$pid")
  BG_PGIDS+=("$pgid")
}

start_background_service() {
  local name=$1
  shift

  enable_background_job_control
  "$@" &
  add_bg_process "$name" "$!"
}

TREE_PIDS=()
append_child_processes() {
  local parent_pid=$1
  local child_pid

  if ! command -v pgrep >/dev/null 2>&1; then
    return
  fi

  while IFS= read -r child_pid; do
    if [[ -z "$child_pid" ]]; then
      continue
    fi
    TREE_PIDS+=("$child_pid")
    append_child_processes "$child_pid"
  done < <(pgrep -P "$parent_pid" 2>/dev/null || true)
}

record_process_tree() {
  local root_pid=$1
  local index

  RECORDED_CLEANUP_PIDS+=("$root_pid")
  TREE_PIDS=()
  append_child_processes "$root_pid"

  for ((index = 0; index < ${#TREE_PIDS[@]}; index++)); do
    RECORDED_CLEANUP_PIDS+=("${TREE_PIDS[$index]}")
  done
}

kill_recorded_process_tree() {
  local root_pid=$1
  local signal=$2
  local index

  for ((index = ${#TREE_PIDS[@]} - 1; index >= 0; index--)); do
    kill "-$signal" "${TREE_PIDS[$index]}" 2>/dev/null || true
  done
  kill "-$signal" "$root_pid" 2>/dev/null || true
}

stop_bg_process() {
  local name=$1
  local pid=$2
  local pgid=$3
  local signal=$4

  record_process_tree "$pid"

  if [[ -n "$pgid" ]]; then
    echo "  - $name: kill -$signal process group $pgid"
    kill "-$signal" -- "-$pgid" 2>/dev/null || true
  fi

  if kill -0 "$pid" 2>/dev/null; then
    echo "  - $name: kill -$signal pid $pid"
  fi
  kill_recorded_process_tree "$pid" "$signal"
}

has_running_bg_processes() {
  local index
  for ((index = 0; index < ${#BG_PGIDS[@]}; index++)); do
    if [[ -n "${BG_PGIDS[$index]}" ]] && kill -0 -- "-${BG_PGIDS[$index]}" 2>/dev/null; then
      return 0
    fi
  done

  for ((index = 0; index < ${#BG_PIDS[@]}; index++)); do
    if kill -0 "${BG_PIDS[$index]}" 2>/dev/null; then
      return 0
    fi
  done

  for ((index = 0; index < ${#RECORDED_CLEANUP_PIDS[@]}; index++)); do
    if kill -0 "${RECORDED_CLEANUP_PIDS[$index]}" 2>/dev/null; then
      return 0
    fi
  done

  return 1
}

cleanup() {
  local exit_code=${1:-$?}
  local index

  if ((CLEANUP_STARTED == 1)); then
    return "$exit_code"
  fi
  CLEANUP_STARTED=1

  if ((${#BG_PIDS[@]} == 0)); then
    return "$exit_code"
  fi

  echo
  echo "==> 正在停止后台服务..."
  for ((index = ${#BG_PIDS[@]} - 1; index >= 0; index--)); do
    stop_bg_process "${BG_NAMES[$index]}" "${BG_PIDS[$index]}" "${BG_PGIDS[$index]}" TERM
  done

  for _ in 1 2 3 4 5 6 7 8 9 10; do
    if ! has_running_bg_processes; then
      return "$exit_code"
    fi
    sleep 0.2
  done

  echo "==> 后台服务未完全退出，强制结束残留进程..."
  for ((index = ${#BG_PIDS[@]} - 1; index >= 0; index--)); do
    stop_bg_process "${BG_NAMES[$index]}" "${BG_PIDS[$index]}" "${BG_PGIDS[$index]}" KILL
  done

  return "$exit_code"
}

handle_interrupt() {
  cleanup 130
  exit 130
}

handle_terminate() {
  cleanup 143
  exit 143
}

trap 'cleanup "$?"' EXIT
trap handle_interrupt INT
trap handle_terminate TERM

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
  echo "  1. 启动 API           (Radish.Api              @ http://localhost:5100)"
  echo "  2. 启动 Gateway       (Radish.Gateway          @ https://localhost:5000)"
  echo "  3. 启动 Frontend      (Frontend/radish.client  @ http://localhost:3000)"
  echo "  4. 启动 Console       (Frontend/radish.console @ http://localhost:3100)"
  echo "  5. 启动 Auth          (Radish.Auth             @ http://localhost:5200)"
  echo "  6. 运行 DbMigrate     (Radish.DbMigrate        @ apply/doctor)"
  echo "  7. 运行单元测试       (Radish.Api.Tests)"
  echo
  echo "[组合启动]"
  echo "  8. 启动 Gateway + Auth + API"
  echo "  9. 启动 Frontend + Console"
  echo
  echo "[全部启动]"
  echo " 10. 一键启动全部服务 (Gateway + API + Auth + Frontend + Console)"
  echo
  echo "提示: 组合启动会将相关服务置于后台运行以便并行开发。"
  echo
}

build_all() {
  ensure_dotnet
  ( cd "$ROOT_DIR" && invoke_step "dotnet build Radish.slnx ($CONFIGURATION)" dotnet build Radish.slnx -c "$CONFIGURATION" )
}

start_api() {
  ensure_dotnet
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
  ensure_dotnet
  ( cd "$ROOT_DIR" && export ASPNETCORE_URLS="http://localhost:5100" && invoke_step "dotnet run API (no-build, http only)" dotnet run --no-build --project "$API_PROJECT" -c "$CONFIGURATION" --launch-profile http )
}

start_gateway() { ensure_dotnet; ( cd "$ROOT_DIR" && dotnet run --project Radish.Gateway/Radish.Gateway.csproj --launch-profile https ); }
start_gateway_no_build() { ensure_dotnet; ( cd "$ROOT_DIR" && exec dotnet run --no-build --project Radish.Gateway/Radish.Gateway.csproj --launch-profile https ); }
start_frontend() { ensure_npm; ( cd "$ROOT_DIR" && exec npm run dev --prefix "$CLIENT_DIR" ); }
start_console() { ensure_npm; ( cd "$ROOT_DIR" && exec npm run dev --prefix "$CONSOLE_DIR" ); }

start_auth() {
  ensure_dotnet
  (
    cd "$ROOT_DIR"
    export ASPNETCORE_URLS="http://localhost:5200"
    invoke_step "dotnet clean ($CONFIGURATION)" dotnet clean "$AUTH_PROJECT" -c "$CONFIGURATION"
    invoke_step "dotnet restore" dotnet restore "$AUTH_PROJECT"
    invoke_step "dotnet build ($CONFIGURATION)" dotnet build "$AUTH_PROJECT" -c "$CONFIGURATION"
    invoke_step "dotnet run (http only)" dotnet run --project "$AUTH_PROJECT" -c "$CONFIGURATION" --launch-profile http
  )
}

start_auth_no_build() { ensure_dotnet; ( cd "$ROOT_DIR" && export ASPNETCORE_URLS="http://localhost:5200" && exec dotnet run --no-build --project "$AUTH_PROJECT" -c "$CONFIGURATION" --launch-profile http ); }

start_dbmigrate() {
  ensure_dotnet
  (
    cd "$ROOT_DIR"
    echo
    echo "DbMigrate 命令说明："
    echo "  apply  - 推荐，自动补齐表结构并填充初始数据"
    echo "  doctor - 只读检查当前环境与主库业务表"
    echo "  init   - 高级命令，仅初始化数据库表结构（Code First）"
    echo "  seed   - 高级命令，填充初始数据（如表不存在会自动执行 init）"
    echo
    read -rp "请选择 DbMigrate 子命令 [apply/doctor/init/seed] (默认 apply, 输入 q 取消): " migrate_cmd
    if [[ -z "${migrate_cmd:-}" ]]; then migrate_cmd="apply"; fi
    if [[ "$migrate_cmd" == "q" ]]; then echo "已取消执行 DbMigrate。"; return; fi
    case "$migrate_cmd" in
      apply|doctor|init|seed) invoke_step "dotnet run DbMigrate ($migrate_cmd, $CONFIGURATION)" dotnet run --project "$DBMIGRATE_PROJECT" -c "$CONFIGURATION" -- "$migrate_cmd" ;;
      *) echo "未知 DbMigrate 子命令: $migrate_cmd" >&2; exit 1 ;;
    esac
  )
}

run_tests() { ensure_dotnet; ( cd "$ROOT_DIR" && invoke_step "dotnet test (Radish.Api.Tests)" dotnet test "$TEST_PROJECT" -c "$CONFIGURATION" ); }

start_gateway_auth_api() {
  echo "[组合] 启动 Gateway + Auth + API..."
  build_all
  start_background_service "Gateway" start_gateway_no_build
  echo "  - Gateway 已在后台启动 (https://localhost:5000)."
  start_background_service "Auth" start_auth_no_build
  echo "  - Auth 已在后台启动 (http://localhost:5200)."
  start_api_no_build
}

start_all() {
  echo "[组合] 一键启动全部服务 (Gateway + API + Auth + Frontend + Console)..."
  build_all
  start_background_service "Gateway" start_gateway_no_build
  echo "  - Gateway 已在后台启动 (https://localhost:5000)."
  start_background_service "Auth" start_auth_no_build
  echo "  - Auth 已在后台启动 (http://localhost:5200)."
  start_background_service "Frontend" start_frontend
  echo "  - Frontend 已在后台启动 (http://localhost:3000)."
  start_background_service "Console" start_console
  echo "  - Console 已在后台启动 (http://localhost:3100)."
  start_api_no_build
}

start_frontend_console() {
  echo "[组合] 启动 Frontend + Console..."
  start_background_service "Frontend" start_frontend
  echo "  - Frontend 已在后台启动 (http://localhost:3000)."
  start_console
}

print_banner
print_menu
read -rp "请输入选项编号: " choice

case "$choice" in
  0) echo "已退出。"; exit 0 ;;
  1) start_api ;;
  2) start_gateway ;;
  3) start_frontend ;;
  4) start_console ;;
  5) start_auth ;;
  6) start_dbmigrate ;;
  7) run_tests ;;
  8) start_gateway_auth_api ;;
  9) start_frontend_console ;;
  10) start_all ;;
  *) echo "Unknown option: $choice" >&2; exit 1 ;;
esac
