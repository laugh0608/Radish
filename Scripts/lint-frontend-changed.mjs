import { spawnSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import process from 'node:process';

const args = new Set(process.argv.slice(2));
const repoRoot = process.cwd();

const workspaceDefinitions = [
  {
    name: '@radish/ui',
    prefix: 'Frontend/radish.ui/',
    root: 'Frontend/radish.ui'
  },
  {
    name: 'radish.client',
    prefix: 'Frontend/radish.client/',
    root: 'Frontend/radish.client'
  },
  {
    name: 'radish.console',
    prefix: 'Frontend/radish.console/',
    root: 'Frontend/radish.console'
  }
];

const eslintExtensions = new Set(['.js', '.jsx', '.mjs', '.cjs', '.ts', '.tsx']);

function parseFiles() {
  const fileArgs = process.argv.slice(2).filter((arg) => !arg.startsWith('--'));

  if (fileArgs.length > 0) {
    return fileArgs;
  }

  const stdin = process.stdin.isTTY
    ? ''
    : readFileSync(process.stdin.fd, args.has('--stdin-z') ? null : 'utf8');

  const chunks = args.has('--stdin-z')
    ? stdin.toString('utf8').split('\0')
    : stdin.toString().split(/\r?\n/u);

  return chunks.map((file) => file.trim()).filter(Boolean);
}

function groupFiles(files) {
  const groups = workspaceDefinitions.map((workspace) => ({
    ...workspace,
    files: []
  }));

  for (const file of files) {
    const normalized = file.replace(/\\/g, '/');
    const extension = path.extname(normalized).toLowerCase();

    if (!eslintExtensions.has(extension)) {
      continue;
    }

    const workspace = groups.find((item) => normalized.startsWith(item.prefix));
    if (!workspace) {
      continue;
    }

    workspace.files.push(path.relative(workspace.root, normalized));
  }

  return groups.filter((workspace) => workspace.files.length > 0);
}

function runLint(workspace) {
  console.log(`\n[frontend-lint] ${workspace.name}`);
  console.log(`[frontend-lint] 文件数：${workspace.files.length}`);

  const result = spawnSync(
    'npm',
    ['exec', '--workspace', workspace.name, 'eslint', '--', ...workspace.files],
    {
      cwd: repoRoot,
      stdio: 'inherit',
      shell: process.platform === 'win32'
    }
  );

  if (result.error) {
    console.error(`[frontend-lint] ${workspace.name} 执行失败：${result.error.message}`);
    process.exit(1);
  }

  if (result.status !== 0) {
    process.exit(result.status ?? 1);
  }
}

function main() {
  const files = parseFiles();
  const groups = groupFiles(files);

  if (groups.length === 0) {
    console.log('[frontend-lint] 本次变更没有需要 lint 的前端脚本文件。');
    return;
  }

  for (const group of groups) {
    runLint(group);
  }
}

main();
