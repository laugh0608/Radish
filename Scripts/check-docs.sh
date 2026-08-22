#!/usr/bin/env bash

set -euo pipefail

script_directory="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repository_root="$(cd -- "${script_directory}/.." && pwd)"

cd "${repository_root}"

if [[ ! -d Docs ]]; then
  echo "[check-docs] 未找到 Docs/ 目录。" >&2
  exit 1
fi

community_documents=(
  README.md
  SECURITY.md
  CONTRIBUTING.md
  CODE_OF_CONDUCT.md
  AGENTS.md
  CLAUDE.md
  LICENSE
)

for document_path in "${community_documents[@]}"; do
  if [[ ! -f "${document_path}" ]]; then
    echo "[check-docs] 缺少根目录治理文档：${document_path}" >&2
    exit 1
  fi
done

echo "[check-docs] 检查 Agent 根入口正文同步。"
if ! cmp -s <(tail -n +4 AGENTS.md) <(tail -n +4 CLAUDE.md); then
  echo "[check-docs] AGENTS.md 与 CLAUDE.md 从第 4 行开始必须完全一致。" >&2
  diff -u <(tail -n +4 AGENTS.md) <(tail -n +4 CLAUDE.md) >&2 || true
  exit 1
fi

echo "[check-docs] 检查 Docs/ 与根目录治理文档的 UTF-8、BOM、疑似乱码、换行和文本卫生。"
{
  find Docs -type f -print0
  printf '%s\0' "${community_documents[@]}"
} | node Scripts/check-repo-hygiene.mjs --stdin-z --skip-document-length-warnings

echo "[check-docs] 检查 Markdown 本地相对链接。"
{
  find Docs -type f -name '*.md' -print0
  printf '%s\0' README.md SECURITY.md CONTRIBUTING.md CODE_OF_CONDUCT.md AGENTS.md CLAUDE.md
} | node Scripts/check-markdown-links.mjs --stdin-z
