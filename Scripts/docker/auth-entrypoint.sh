#!/bin/sh

set -eu

. /app/scripts/cert-utils.sh

maybe_generate_auth_certificate() {
  cert_kind="$1"
  cert_path="$2"
  cert_password="$3"
  cert_subject="$4"
  valid_days="$5"

  if [ -f "$cert_path" ]; then
    log_cert "复用已有 Auth ${cert_kind} 证书: $cert_path"
    return 0
  fi

  if [ -z "$cert_password" ]; then
    log_cert "缺少 Auth ${cert_kind} 证书密码，无法自动生成。"
    exit 1
  fi

  log_cert "生成 Auth ${cert_kind} 证书: $cert_path"
  generate_generic_pfx "$cert_path" "$cert_password" "$cert_subject" "$valid_days"
}

if is_true "${RADISH_AUTH_CERT_AUTO_GENERATE:-false}"; then
  signing_path="${OpenIddict__Encryption__SigningCertificatePath:-}"
  signing_password="${OpenIddict__Encryption__SigningCertificatePassword:-}"
  encryption_path="${OpenIddict__Encryption__EncryptionCertificatePath:-}"
  encryption_password="${OpenIddict__Encryption__EncryptionCertificatePassword:-}"
  valid_days="${RADISH_AUTH_CERT_VALID_DAYS:-3650}"

  if [ -z "$signing_path" ] || [ -z "$encryption_path" ]; then
    log_cert "OpenIddict 证书路径未配置，无法自动生成 Auth OIDC 证书。"
    exit 1
  fi

  maybe_generate_auth_certificate \
    "signing" \
    "$signing_path" \
    "$signing_password" \
    "${RADISH_AUTH_SIGNING_CERT_SUBJECT:-radish-auth-signing}" \
    "$valid_days"

  maybe_generate_auth_certificate \
    "encryption" \
    "$encryption_path" \
    "$encryption_password" \
    "${RADISH_AUTH_ENCRYPTION_CERT_SUBJECT:-radish-auth-encryption}" \
    "$valid_days"
fi

exec dotnet Radish.Auth.dll
