#!/bin/sh

set -eu

. /app/scripts/cert-utils.sh

if is_true "${RADISH_GATEWAY_CERT_AUTO_GENERATE:-false}"; then
  cert_path="${Kestrel__Certificates__Default__Path:-}"
  cert_password="${Kestrel__Certificates__Default__Password:-}"
  cert_host="${RADISH_GATEWAY_CERT_HOST:-}"
  public_url="${RADISH_PUBLIC_URL:-${GatewayService__PublicUrl:-}}"
  valid_days="${RADISH_GATEWAY_CERT_VALID_DAYS:-365}"

  if [ -z "$cert_path" ] || [ -z "$cert_password" ]; then
    log_cert "Gateway TLS 证书路径或密码缺失，无法自动生成。"
    exit 1
  fi

  if [ -f "$cert_path" ]; then
    log_cert "复用已有 Gateway TLS 证书: $cert_path"
  else
    if [ -z "$cert_host" ]; then
      cert_host="$(resolve_url_host "$public_url")"
    fi

    if [ -z "$cert_host" ]; then
      log_cert "无法从 Gateway 公开地址推导 Gateway TLS 证书主机名。RADISH_PUBLIC_URL='${RADISH_PUBLIC_URL:-}' GatewayService__PublicUrl='${GatewayService__PublicUrl:-}'"
      exit 1
    fi

    cert_subject="${RADISH_GATEWAY_CERT_SUBJECT:-$cert_host}"

    log_cert "生成 Gateway TLS 证书: $cert_path (host=$cert_host)"
    generate_tls_pfx "$cert_path" "$cert_password" "$cert_subject" "$cert_host" "$valid_days"
  fi
fi

exec dotnet Radish.Gateway.dll
