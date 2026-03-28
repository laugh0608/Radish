#!/bin/sh

set -eu

log_cert() {
  echo "[cert-init] $*"
}

is_true() {
  value="$(printf '%s' "${1:-}" | tr '[:upper:]' '[:lower:]')"
  case "$value" in
    1|true|yes|on)
      return 0
      ;;
    *)
      return 1
      ;;
  esac
}

ensure_parent_dir() {
  file_path="$1"
  mkdir -p "$(dirname "$file_path")"
}

resolve_url_host() {
  raw_url="${1:-}"

  if [ -z "$raw_url" ]; then
    echo ""
    return 0
  fi

  host_part="${raw_url#*://}"
  host_part="${host_part%%/*}"

  case "$host_part" in
    \[*\]*)
      host_value="${host_part#\[}"
      host_value="${host_value%%\]*}"
      ;;
    *:*)
      host_value="${host_part%%:*}"
      ;;
    *)
      host_value="$host_part"
      ;;
  esac

  echo "$host_value"
}

looks_like_ipv4() {
  printf '%s' "$1" | grep -Eq '^[0-9]{1,3}(\.[0-9]{1,3}){3}$'
}

generate_generic_pfx() {
  output_path="$1"
  password="$2"
  subject_name="$3"
  valid_days="$4"

  ensure_parent_dir "$output_path"

  tmp_dir="$(mktemp -d)"
  key_path="$tmp_dir/cert.key"
  crt_path="$tmp_dir/cert.crt"

  openssl req \
    -x509 \
    -newkey rsa:4096 \
    -sha256 \
    -nodes \
    -days "$valid_days" \
    -subj "/CN=$subject_name" \
    -keyout "$key_path" \
    -out "$crt_path" >/dev/null 2>&1

  openssl pkcs12 \
    -export \
    -out "$output_path" \
    -inkey "$key_path" \
    -in "$crt_path" \
    -password "pass:$password" >/dev/null 2>&1

  chmod 600 "$output_path"
  rm -rf "$tmp_dir"
}

generate_tls_pfx() {
  output_path="$1"
  password="$2"
  subject_name="$3"
  host_name="$4"
  valid_days="$5"

  ensure_parent_dir "$output_path"

  tmp_dir="$(mktemp -d)"
  key_path="$tmp_dir/cert.key"
  crt_path="$tmp_dir/cert.crt"
  cfg_path="$tmp_dir/openssl.cnf"

  if looks_like_ipv4 "$host_name"; then
    san_entry="IP.1 = $host_name"
  else
    san_entry="DNS.1 = $host_name"
  fi

  cat > "$cfg_path" <<EOF
[req]
distinguished_name = req_distinguished_name
x509_extensions = v3_req
prompt = no

[req_distinguished_name]
CN = $subject_name

[v3_req]
keyUsage = critical, digitalSignature, keyEncipherment
extendedKeyUsage = serverAuth
subjectAltName = @alt_names

[alt_names]
$san_entry
EOF

  openssl req \
    -x509 \
    -newkey rsa:2048 \
    -sha256 \
    -nodes \
    -days "$valid_days" \
    -keyout "$key_path" \
    -out "$crt_path" \
    -config "$cfg_path" >/dev/null 2>&1

  openssl pkcs12 \
    -export \
    -out "$output_path" \
    -inkey "$key_path" \
    -in "$crt_path" \
    -password "pass:$password" >/dev/null 2>&1

  chmod 600 "$output_path"
  rm -rf "$tmp_dir"
}
