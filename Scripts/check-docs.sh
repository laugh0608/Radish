#!/usr/bin/env bash

set -euo pipefail

script_directory="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repository_root="$(cd -- "${script_directory}/.." && pwd)"

cd "${repository_root}"

if [[ ! -d Docs ]]; then
  echo "[check-docs] 未找到 Docs/ 目录。" >&2
  exit 1
fi

echo "[check-docs] 检查 Docs/ 的 UTF-8、BOM、疑似乱码、换行和文本卫生。"
find Docs -type f -print0 | node Scripts/check-repo-hygiene.mjs --stdin-z --skip-document-length-warnings
