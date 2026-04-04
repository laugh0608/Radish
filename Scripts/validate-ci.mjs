import { spawnSync } from 'node:child_process';
import process from 'node:process';

import { collectIdentityImpactMatches } from './identity-impact-rules.mjs';
import {
  IDENTITY_GUARD_CHECK_NAME,
  IDENTITY_GUARD_VALIDATE_ARGS,
  REPO_QUALITY_LOCAL_STEPS,
} from './repo-quality-contract.mjs';

const repoRoot = process.cwd();

function runNode(commandArgs, options = {}) {
  const needsCmdWrapper = process.platform === 'win32';
  const result = spawnSync(needsCmdWrapper ? 'cmd.exe' : 'node', needsCmdWrapper ? ['/d', '/s', '/c', ['node', ...commandArgs].join(' ')] : commandArgs, {
    cwd: repoRoot,
    stdio: options.captureStdout ? ['ignore', 'pipe', 'inherit'] : 'inherit',
    shell: false,
  });

  if (result.error) {
    console.error(`[validate:ci] 执行失败：${result.error.message}`);
    process.exit(1);
  }

  if (result.status !== 0) {
    process.exit(result.status ?? 1);
  }

  return result.stdout ?? Buffer.alloc(0);
}

function runNpm(title, args) {
  console.log(`\n[validate:ci] ${title}`);
  console.log(`> npm ${args.join(' ')}`);

  const result = spawnSync('cmd.exe', ['/d', '/s', '/c', ['npm', ...args].join(' ')], {
    cwd: repoRoot,
    stdio: 'inherit',
    shell: false,
  });

  if (result.error) {
    console.error(`[validate:ci] ${title} 执行失败：${result.error.message}`);
    process.exit(1);
  }

  if (result.status !== 0) {
    process.exit(result.status ?? 1);
  }
}

function splitZeroTerminated(buffer) {
  return buffer
    .toString('utf8')
    .split('\0')
    .map((file) => file.trim())
    .filter(Boolean);
}

console.log('[validate:ci] 模式：repo-quality-local');

for (const step of REPO_QUALITY_LOCAL_STEPS) {
  runNpm(step.title, step.npmArgs);
}

const changedFilesOutput = runNode(['Scripts/collect-changed-files.mjs'], { captureStdout: true });
const changedFiles = splitZeroTerminated(changedFilesOutput);
const matchedFiles = collectIdentityImpactMatches(changedFiles);

console.log(`\n[validate:ci] ${IDENTITY_GUARD_CHECK_NAME} changed-only 判定`);
console.log(`- 当前变更文件：${changedFiles.length} 个`);
console.log(`- 命中身份语义影响面：${matchedFiles.length} 个`);

if (matchedFiles.length === 0) {
  console.log('- 结果：跳过 `validate:identity`，与当前 Repo Quality / Identity Guard 一致。');
  process.exit(0);
}

for (const matchedFile of matchedFiles) {
  console.log(`  - ${matchedFile}`);
}

runNpm('Identity Regression Validation', IDENTITY_GUARD_VALIDATE_ARGS);
