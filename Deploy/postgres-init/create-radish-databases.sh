#!/usr/bin/env bash
set -euo pipefail

create_database() {
  local database_name="$1"

  if [[ -z "$database_name" ]]; then
    return
  fi

  psql \
    --username "$POSTGRES_USER" \
    --dbname "$POSTGRES_DB" \
    --set=database_name="$database_name" <<'SQL'
SELECT format('CREATE DATABASE %I', :'database_name')
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = :'database_name')\gexec
SQL
}

create_database "${RADISH_POSTGRES_LOG_DB:-radish_log}"
create_database "${RADISH_POSTGRES_MESSAGE_DB:-radish_message}"
create_database "${RADISH_POSTGRES_CHAT_DB:-radish_chat}"
