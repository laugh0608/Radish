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

start_frontend() {
  (cd "$ROOT_DIR" && npm run dev --prefix "$CLIENT_DIR")
}

start_console() {
  (cd "$ROOT_DIR" && npm run dev --prefix "$CONSOLE_DIR")
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

echo "Select an action:"
echo "1. Start frontend (radish.client)"
echo "2. Start backend (Radish.Api)"
echo "3. Start both frontend and backend"
echo "4. Start Gateway (Radish.Gateway)"
echo "5. Start docs (radish.docs)"
echo "6. Run unit tests (Radish.Api.Tests)"
echo "7. Start console (radish.console)"
read -rp "Enter choice (1/2/3/4/5/6/7): " choice

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
    echo "Frontend started in background (PID: $FRONTEND_PID). Press Ctrl+C here to stop backend."
    start_backend
    ;;
  4)
    (
      cd "$ROOT_DIR"
      dotnet run --project Radish.Gateway/Radish.Gateway.csproj
    )
    ;;
  5)
    (
      cd "$ROOT_DIR"
      npm run docs:dev --prefix radish.docs
    )
    ;;
  6)
    run_tests
    ;;
  7)
    start_console
    ;;
  *)
    echo "Unknown option: $choice" >&2
    exit 1
    ;;
esac
