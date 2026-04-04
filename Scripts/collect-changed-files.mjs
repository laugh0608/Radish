import { spawnSync } from 'node:child_process';
import { writeFileSync } from 'node:fs';
import process from 'node:process';

const rawArgs = process.argv.slice(2);
const repoRoot = process.cwd();

function readOption(name) {
  const prefix = `${name}=`;
  const matched = rawArgs.find((arg) => arg.startsWith(prefix));
  return matched ? matched.slice(prefix.length) : null;
}

function hasFlag(flag) {
  return rawArgs.includes(flag);
}

function unique(values) {
  return [...new Set(values)];
}

function splitZeroTerminated(buffer) {
  return buffer
    .toString('utf8')
    .split('\0')
    .map((file) => file.trim())
    .filter(Boolean);
}

function runGit(args) {
  const result = spawnSync('git', args, {
    cwd: repoRoot,
    encoding: null,
    stdio: ['ignore', 'pipe', 'pipe'],
    shell: false,
  });

  if (result.error) {
    console.error(`[collect-changed-files] git ${args.join(' ')} 执行失败：${result.error.message}`);
    process.exit(1);
  }

  if (result.status !== 0) {
    const stderr = result.stderr?.toString('utf8').trim();
    console.error(`[collect-changed-files] git ${args.join(' ')} 失败。`);
    if (stderr) {
      console.error(stderr);
    }
    process.exit(result.status ?? 1);
  }

  return splitZeroTerminated(result.stdout ?? Buffer.alloc(0));
}

function collectFiles() {
  const base = readOption('--base');
  const head = readOption('--head');
  const mode = readOption('--mode') ?? 'worktree';

  if ((base && !head) || (!base && head)) {
    console.error('[collect-changed-files] `--base` 与 `--head` 必须同时出现。');
    process.exit(1);
  }

  if (base && head) {
    if (hasFlag('--include-all-on-empty-base') && /^0+$/.test(base)) {
      return runGit(['ls-files', '-z']);
    }

    return runGit(['diff', '--name-only', '--diff-filter=ACMR', '-z', base, head]);
  }

  if (mode === 'staged') {
    return runGit(['diff', '--cached', '--name-only', '--diff-filter=ACMR', '-z']);
  }

  if (mode === 'tracked') {
    return runGit(['ls-files', '-z']);
  }

  return runGit(['diff', '--name-only', '--diff-filter=ACMR', '-z']);
}

function outputFiles(files) {
  const outputPath = readOption('--write');
  const uniqueFiles = unique(files);
  const payload = uniqueFiles.length > 0
    ? `${uniqueFiles.join('\0')}\0`
    : '';

  if (outputPath) {
    writeFileSync(outputPath, payload, 'utf8');
  } else {
    process.stdout.write(payload);
  }
}

function main() {
  const files = collectFiles();
  outputFiles(files);
}

main();
