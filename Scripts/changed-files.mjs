import { runCommand } from './process-runner.mjs';

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

function runGit(repoRoot, args) {
  const result = runCommand('git', args, {
    cwd: repoRoot,
    encoding: null,
    stdio: ['ignore', 'pipe', 'pipe'],
  });

  if (result.error) {
    throw new Error(`[collect-changed-files] git ${args.join(' ')} 执行失败：${result.error.message}`);
  }

  if (result.status !== 0) {
    const stderr = result.stderr?.toString('utf8').trim();
    const extraMessage = stderr ? `\n${stderr}` : '';
    throw new Error(`[collect-changed-files] git ${args.join(' ')} 失败。${extraMessage}`);
  }

  return splitZeroTerminated(result.stdout ?? Buffer.alloc(0));
}

export function collectChangedFiles({
  repoRoot,
  base = null,
  head = null,
  mode = 'worktree',
  includeAllOnEmptyBase = false,
}) {
  if (base && !head) {
    throw new Error('[collect-changed-files] `--base` 与 `--head` 必须同时出现。');
  }

  if (!base && head) {
    if (includeAllOnEmptyBase) {
      return unique(runGit(repoRoot, ['ls-files', '-z']));
    }

    throw new Error('[collect-changed-files] `--base` 与 `--head` 必须同时出现。');
  }

  if (base && head) {
    if (includeAllOnEmptyBase && /^0+$/.test(base)) {
      return unique(runGit(repoRoot, ['ls-files', '-z']));
    }

    return unique(runGit(repoRoot, ['diff', '--name-only', '--diff-filter=ACMR', '-z', base, head]));
  }

  if (includeAllOnEmptyBase) {
    return unique(runGit(repoRoot, ['ls-files', '-z']));
  }

  if (mode === 'staged') {
    return unique(runGit(repoRoot, ['diff', '--cached', '--name-only', '--diff-filter=ACMR', '-z']));
  }

  if (mode === 'tracked') {
    return unique(runGit(repoRoot, ['ls-files', '-z']));
  }

  return unique(runGit(repoRoot, ['diff', '--name-only', '--diff-filter=ACMR', '-z']));
}
