#!/usr/bin/env bash

set -Eeuo pipefail
umask 077

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
COMPOSE_FILE="${RADISH_DEPLOY_COMPOSE_FILE:-${SCRIPT_DIR}/docker-compose.yaml}"
ENV_FILE="${RADISH_DEPLOY_ENV_FILE:-${SCRIPT_DIR}/.env}"
DOCKER_BIN="${RADISH_DEPLOY_DOCKER_BIN:-docker}"
CURL_BIN="${RADISH_DEPLOY_CURL_BIN:-curl}"
DEPLOY_TIMESTAMP="${RADISH_DEPLOY_TIMESTAMP:-$(date -u +%Y%m%dT%H%M%SZ)}"

CONFIRM_PRODUCTION=false
PREFLIGHT_ONLY=false
MIGRATION_STARTED=false
BACKUP_DIR=""
PREVIOUS_APP_SERVICES=""

log() {
    printf '[radish-production-deploy] %s\n' "$*"
}

fail() {
    printf '[radish-production-deploy] ERROR: %s\n' "$*" >&2
    return 1
}

usage() {
    cat <<'EOF'
Usage:
  ./Deploy/deploy-production.sh --preflight-only
  ./Deploy/deploy-production.sh --confirm-production

Options:
  --preflight-only      Validate the production environment and pinned images without changing state.
  --confirm-production Run the complete production sequence.
  --help                Show this help.

Environment overrides:
  RADISH_DEPLOY_ENV_FILE       Compose env file. Default: Deploy/.env
  RADISH_DEPLOY_COMPOSE_FILE   Compose file. Default: Deploy/docker-compose.yaml
  RADISH_DEPLOY_BACKUP_ROOT    Host backup root. Overrides RADISH_BACKUP_PATH from the env file.
EOF
}

while (($# > 0)); do
    case "$1" in
        --confirm-production)
            CONFIRM_PRODUCTION=true
            ;;
        --preflight-only)
            PREFLIGHT_ONLY=true
            ;;
        --help|-h)
            usage
            exit 0
            ;;
        *)
            fail "Unknown argument: $1"
            ;;
    esac
    shift
done

if [[ "${CONFIRM_PRODUCTION}" == "true" && "${PREFLIGHT_ONLY}" == "true" ]]; then
    fail "--confirm-production and --preflight-only cannot be used together."
fi

read_env_value() {
    local key="$1"
    local default_value="${2:-}"
    local value

    if value="$(
        awk -v target_key="${key}" '
            /^[[:space:]]*#/ { next }
            {
                line = $0
                sub(/\r$/, "", line)
                separator = index(line, "=")
                if (separator == 0) {
                    next
                }
                name = substr(line, 1, separator - 1)
                gsub(/^[[:space:]]+|[[:space:]]+$/, "", name)
                if (name == target_key) {
                    print substr(line, separator + 1)
                    found = 1
                }
            }
            END {
                if (!found) {
                    exit 1
                }
            }
        ' "${ENV_FILE}"
    )"; then
        value="$(printf '%s' "${value}" | sed -e 's/^[[:space:]]*//' -e 's/[[:space:]]*$//')"
        if [[ ${#value} -ge 2 ]]; then
            if [[ "${value:0:1}" == '"' && "${value: -1}" == '"' ]] ||
                [[ "${value:0:1}" == "'" && "${value: -1}" == "'" ]]; then
                value="${value:1:${#value}-2}"
            fi
        fi
        printf '%s' "${value}"
        return
    fi

    printf '%s' "${default_value}"
}

require_executable() {
    local executable="$1"
    if [[ "${executable}" == */* ]]; then
        [[ -x "${executable}" ]] || fail "Executable not found: ${executable}"
        return
    fi
    command -v "${executable}" >/dev/null 2>&1 || fail "Executable not found in PATH: ${executable}"
}

file_mode() {
    local path="$1"
    if stat -c '%a' "${path}" >/dev/null 2>&1; then
        stat -c '%a' "${path}"
        return
    fi
    stat -f '%Lp' "${path}"
}

checksum_file() {
    local path="$1"
    if command -v sha256sum >/dev/null 2>&1; then
        sha256sum "$(basename "${path}")"
        return
    fi
    shasum -a 256 "$(basename "${path}")"
}

compose() {
    "${DOCKER_BIN}" compose --env-file "${ENV_FILE}" -f "${COMPOSE_FILE}" "$@"
}

release_record_exists() {
    local tag="$1"
    local record
    for record in "${REPO_ROOT}"/Docs/records/*.md; do
        [[ -f "${record}" ]] || continue
        if grep -Fqx "releaseTag: ${tag}" "${record}" &&
            grep -Fqx "imageTag: ${tag}" "${record}"; then
            return 0
        fi
    done
    return 1
}

validate_preflight() {
    [[ -f "${ENV_FILE}" ]] || fail "Environment file not found: ${ENV_FILE}"
    [[ -f "${COMPOSE_FILE}" ]] || fail "Compose file not found: ${COMPOSE_FILE}"

    local mode
    mode="$(file_mode "${ENV_FILE}")"
    local mode_decimal=$((8#${mode}))
    if ((mode_decimal & 077)); then
        fail "${ENV_FILE} permissions are ${mode}; production secrets require mode 600 or stricter."
    fi

    require_executable "${DOCKER_BIN}"
    require_executable "${CURL_BIN}"

    IMAGE_TAG="$(read_env_value RADISH_IMAGE_TAG)"
    IMAGE_REGISTRY="$(read_env_value RADISH_IMAGE_REGISTRY ghcr.io/laugh0608)"
    IMAGE_TRACK="$(read_env_value RADISH_IMAGE_TRACK release)"
    DEPLOYMENT_STAGE="$(read_env_value RADISH_DEPLOYMENT_STAGE production)"
    DEVELOPER_SEED="$(read_env_value RADISH_SEED_DEVELOPER_DEFAULTS_ENABLED false)"
    PUBLIC_URL="$(read_env_value RADISH_PUBLIC_URL)"
    POSTGRES_USER="$(read_env_value RADISH_POSTGRES_USER)"
    POSTGRES_MAIN_DB="$(read_env_value RADISH_POSTGRES_MAIN_DB radish)"
    POSTGRES_LOG_DB="$(read_env_value RADISH_POSTGRES_LOG_DB radish_log)"
    POSTGRES_MESSAGE_DB="$(read_env_value RADISH_POSTGRES_MESSAGE_DB radish_message)"
    POSTGRES_CHAT_DB="$(read_env_value RADISH_POSTGRES_CHAT_DB radish_chat)"
    POSTGRES_OPENIDDICT_DB="$(read_env_value RADISH_POSTGRES_OPENIDDICT_DB radish_openiddict)"
    POSTGRES_HANGFIRE_DB="$(read_env_value RADISH_POSTGRES_HANGFIRE_DB radish_hangfire)"
    BACKUP_ROOT="${RADISH_DEPLOY_BACKUP_ROOT:-$(read_env_value RADISH_BACKUP_PATH ../DeployBackups)}"

    [[ "${IMAGE_TAG}" =~ ^v[0-9]+\.[0-9]+\.[0-9]+(\.[1-9][0-9]*)?-release$ ]] ||
        fail "RADISH_IMAGE_TAG must be an immutable v*-release tag, not a floating alias."
    [[ "${IMAGE_TRACK}" == "release" ]] ||
        fail "RADISH_IMAGE_TRACK must be release for production deployment."
    [[ "${DEPLOYMENT_STAGE}" == "production" ]] ||
        fail "RADISH_DEPLOYMENT_STAGE must be production."
    [[ "${DEVELOPER_SEED}" == "false" ]] ||
        fail "RADISH_SEED_DEVELOPER_DEFAULTS_ENABLED must be false in production."
    [[ "${PUBLIC_URL}" == https://* ]] ||
        fail "RADISH_PUBLIC_URL must use the external HTTPS origin."
    [[ "${POSTGRES_USER}" =~ ^[A-Za-z_][A-Za-z0-9_.-]*$ ]] ||
        fail "RADISH_POSTGRES_USER contains unsupported characters."
    [[ "${DEPLOY_TIMESTAMP}" =~ ^[0-9]{8}T[0-9]{6}Z$ ]] ||
        fail "RADISH_DEPLOY_TIMESTAMP must use YYYYMMDDTHHMMSSZ."
    [[ -n "${BACKUP_ROOT}" && "${BACKUP_ROOT}" != "/" ]] ||
        fail "Backup root must be a dedicated non-root directory."
    release_record_exists "${IMAGE_TAG}" ||
        fail "No release record matches RADISH_IMAGE_TAG=${IMAGE_TAG}."

    local database_name
    for database_name in \
        "${POSTGRES_MAIN_DB}" \
        "${POSTGRES_LOG_DB}" \
        "${POSTGRES_MESSAGE_DB}" \
        "${POSTGRES_CHAT_DB}" \
        "${POSTGRES_OPENIDDICT_DB}" \
        "${POSTGRES_HANGFIRE_DB}"; do
        [[ "${database_name}" =~ ^[A-Za-z_][A-Za-z0-9_.-]*$ ]] ||
            fail "Database name contains unsupported characters: ${database_name}"
    done

    compose version >/dev/null
    compose config >/dev/null

    local configured_images
    configured_images="$(compose config --images)"
    local image_name
    for image_name in radish-dbmigrate radish-frontend radish-api radish-auth radish-gateway; do
        local expected_image="${IMAGE_REGISTRY}/${image_name}:${IMAGE_TAG}"
        printf '%s\n' "${configured_images}" | grep -Fqx "${expected_image}" ||
            fail "Compose does not resolve ${image_name} to pinned image ${expected_image}."
    done

    log "Preflight passed for ${IMAGE_TAG}."
}

service_was_running() {
    local target="$1"
    printf '%s\n' "${PREVIOUS_APP_SERVICES}" | grep -Fqx "${target}"
}

restart_previous_apps() {
    local services=()
    local service
    for service in frontend api auth gateway; do
        if service_was_running "${service}"; then
            services+=("${service}")
        fi
    done

    if ((${#services[@]} == 0)); then
        return
    fi

    log "Restarting the previously running application version after a pre-migration failure."
    compose start "${services[@]}"
}

on_error() {
    local exit_code=$?
    trap - ERR
    set +e
    if [[ -n "${BACKUP_DIR}" && -d "${BACKUP_DIR}" ]]; then
        printf 'failed\n' > "${BACKUP_DIR}/DEPLOY_FAILED"
    fi
    if [[ "${MIGRATION_STARTED}" == "false" ]]; then
        restart_previous_apps
    else
        compose stop gateway api auth frontend
        log "Migration had started; application services remain stopped for explicit operator review."
    fi
    if [[ -n "${BACKUP_DIR}" ]]; then
        log "Preserved deployment backup and evidence at ${BACKUP_DIR}."
    fi
    exit "${exit_code}"
}

wait_for_running_services() {
    local attempts=30
    local attempt
    for ((attempt = 1; attempt <= attempts; attempt++)); do
        local running_services
        running_services="$(compose ps --status running --services)"
        local missing=false
        local service
        for service in "$@"; do
            if ! printf '%s\n' "${running_services}" | grep -Fqx "${service}"; then
                missing=true
                break
            fi
        done
        if [[ "${missing}" == "false" ]]; then
            return
        fi
        sleep 2
    done
    fail "Services did not reach running state: $*"
}

create_database_backup() {
    if [[ "${BACKUP_ROOT}" != /* ]]; then
        BACKUP_ROOT="${SCRIPT_DIR}/${BACKUP_ROOT}"
    fi
    mkdir -p "${BACKUP_ROOT}"
    BACKUP_ROOT="$(cd "${BACKUP_ROOT}" && pwd)"
    BACKUP_DIR="${BACKUP_ROOT}/${DEPLOY_TIMESTAMP}-${IMAGE_TAG}"
    mkdir "${BACKUP_DIR}" || fail "Backup directory already exists: ${BACKUP_DIR}"

    log "Creating PostgreSQL backup in ${BACKUP_DIR}."
    compose exec -T postgres pg_dumpall \
        --username="${POSTGRES_USER}" \
        --globals-only \
        --no-role-passwords > "${BACKUP_DIR}/globals.sql"
    [[ -s "${BACKUP_DIR}/globals.sql" ]] ||
        fail "PostgreSQL globals backup is empty."

    local labels=(main log message chat openiddict hangfire)
    local databases=(
        "${POSTGRES_MAIN_DB}"
        "${POSTGRES_LOG_DB}"
        "${POSTGRES_MESSAGE_DB}"
        "${POSTGRES_CHAT_DB}"
        "${POSTGRES_OPENIDDICT_DB}"
        "${POSTGRES_HANGFIRE_DB}"
    )
    local index
    for ((index = 0; index < ${#databases[@]}; index++)); do
        local backup_file="${BACKUP_DIR}/${labels[index]}.dump"
        compose exec -T postgres pg_dump \
            --username="${POSTGRES_USER}" \
            --dbname="${databases[index]}" \
            --format=custom \
            --no-owner \
            --no-privileges > "${backup_file}"
        [[ -s "${backup_file}" ]] ||
            fail "Backup is empty for database ${databases[index]}."
        compose exec -T postgres pg_restore --list < "${backup_file}" >/dev/null
    done

    (
        cd "${BACKUP_DIR}"
        : > SHA256SUMS
        local backup_file
        for backup_file in globals.sql main.dump log.dump message.dump chat.dump openiddict.dump hangfire.dump; do
            checksum_file "${backup_file}" >> SHA256SUMS
        done
    )

    {
        printf 'release_tag=%s\n' "${IMAGE_TAG}"
        printf 'created_at_utc=%s\n' "${DEPLOY_TIMESTAMP}"
        printf 'public_url=%s\n' "${PUBLIC_URL}"
        printf 'database_main=%s\n' "${POSTGRES_MAIN_DB}"
        printf 'database_log=%s\n' "${POSTGRES_LOG_DB}"
        printf 'database_message=%s\n' "${POSTGRES_MESSAGE_DB}"
        printf 'database_chat=%s\n' "${POSTGRES_CHAT_DB}"
        printf 'database_openiddict=%s\n' "${POSTGRES_OPENIDDICT_DB}"
        printf 'database_hangfire=%s\n' "${POSTGRES_HANGFIRE_DB}"
    } > "${BACKUP_DIR}/metadata.env"
}

run_production_deployment() {
    [[ "${CONFIRM_PRODUCTION}" == "true" ]] ||
        fail "State-changing deployment requires --confirm-production."

    trap on_error ERR

    log "Pulling the five pinned application images before the maintenance window."
    compose pull dbmigrate frontend api auth gateway

    log "Ensuring PostgreSQL and Redis are healthy."
    compose up -d --wait postgres redis

    PREVIOUS_APP_SERVICES="$(compose ps --status running --services)"
    log "Stopping application writers before the cross-database backup."
    compose stop gateway api auth frontend

    create_database_backup

    MIGRATION_STARTED=true
    log "Running the explicit one-shot migration job."
    compose run --rm --no-deps dbmigrate apply
    log "Running an independent strict migration verification."
    compose run --rm --no-deps dbmigrate verify

    log "Starting the pinned frontend, API and Auth images."
    compose up -d --no-deps --force-recreate frontend api auth
    wait_for_running_services frontend api auth

    log "Starting Gateway after its downstream services are running."
    compose up -d --no-deps --force-recreate gateway
    wait_for_running_services gateway

    log "Checking the external Gateway health endpoint."
    "${CURL_BIN}" \
        --fail \
        --silent \
        --show-error \
        --max-time 10 \
        --retry 12 \
        --retry-delay 5 \
        --retry-all-errors \
        "${PUBLIC_URL%/}/health" >/dev/null

    printf 'succeeded\n' > "${BACKUP_DIR}/DEPLOY_SUCCEEDED"
    trap - ERR
    log "Production deployment completed for ${IMAGE_TAG}."
    log "Backup and recovery anchor: ${BACKUP_DIR}"
}

validate_preflight

if [[ "${PREFLIGHT_ONLY}" == "true" ]]; then
    exit 0
fi

run_production_deployment
