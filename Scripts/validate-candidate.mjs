import process from 'node:process';

import { formatCommand, runCommand } from './process-runner.mjs';

const repoRoot = process.cwd();
const steps = [
  ['全量仓库卫生预算', ['run', 'check:repo-hygiene:candidate']],
  ['全量前端零 warning lint', ['run', 'lint']],
  ['候选 baseline', ['run', 'validate:baseline', '--', '--warnings-as-errors']],
  ['外部 LongId 字符串安全', ['run', 'check:long-id-safety']],
  ['依赖安全', ['run', 'check:dependency-security']],
];

for (const [title, args] of steps) {
  console.log(`\n[candidate] ${title}`);
  console.log(`> ${formatCommand('npm', args)}`);
  const result = runCommand('npm', args, {
    cwd: repoRoot,
    stdio: 'inherit',
  });

  if (result.error) {
    console.error(`[candidate] ${title} 执行失败：${result.error.message}`);
    process.exit(1);
  }

  if (result.status !== 0) {
    process.exit(result.status ?? 1);
  }
}

console.log('\n[candidate] 候选质量验证已完成。');
