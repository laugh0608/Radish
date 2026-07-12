import { existsSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import process from 'node:process';

import { runCommand } from './process-runner.mjs';

const repoRoot = process.cwd();
const baselinePath = path.join(repoRoot, 'Scripts', 'time-semantics-baseline.json');
const writeBaseline = process.argv.includes('--write-baseline');
const forbiddenPattern = /\b(?:DateTime\.(?:Now|Today)|DateTimeOffset\.Now)\b/g;
const sourceRoots = [
  'Radish.Api/',
  'Radish.Auth/',
  'Radish.Common/',
  'Radish.DbMigrate/',
  'Radish.Extension/',
  'Radish.Extension.Log/',
  'Radish.Gateway/',
  'Radish.Infrastructure/',
  'Radish.Model/',
  'Radish.Repository/',
  'Radish.Service/',
];

function collectSourceFiles() {
  const result = runCommand('git', ['ls-files', '-co', '--exclude-standard'], {
    cwd: repoRoot,
    stdio: ['ignore', 'pipe', 'inherit'],
  });

  if (result.error || result.status !== 0) {
    console.error(`[time-semantics] 源文件收集失败：${result.error?.message ?? `exit ${result.status}`}`);
    process.exit(result.status ?? 1);
  }

  const output = Buffer.isBuffer(result.stdout)
    ? result.stdout.toString('utf8')
    : String(result.stdout ?? '');

  return output
    .split(/\r?\n/u)
    .map((item) => item.trim())
    .filter((item) => item.endsWith('.cs'))
    .filter((item) => sourceRoots.some((root) => item.startsWith(root)))
    .filter((item) => !item.includes('/bin/') && !item.includes('/obj/'))
    .sort((left, right) => left.localeCompare(right));
}

function collectCounts(files) {
  const counts = {};
  for (const filePath of files) {
    const absolutePath = path.join(repoRoot, filePath);
    if (!existsSync(absolutePath)) {
      continue;
    }

    const content = readFileSync(absolutePath, 'utf8');
    const count = [...content.matchAll(forbiddenPattern)].length;
    if (count > 0) {
      counts[filePath] = count;
    }
  }

  return counts;
}

function sortCounts(counts) {
  return Object.fromEntries(
    Object.entries(counts).sort(([left], [right]) => left.localeCompare(right))
  );
}

const files = collectSourceFiles();
const current = sortCounts(collectCounts(files));

if (writeBaseline) {
  writeFileSync(baselinePath, `${JSON.stringify(current, null, 2)}\n`, 'utf8');
  console.log(`[time-semantics] 已刷新 baseline：${Object.keys(current).length} 个文件。`);
  process.exit(0);
}

if (!existsSync(baselinePath)) {
  console.error('[time-semantics] 缺少 Scripts/time-semantics-baseline.json。');
  process.exit(1);
}

const baseline = JSON.parse(readFileSync(baselinePath, 'utf8'));
const regressions = [];
let currentTotal = 0;
let baselineTotal = 0;

for (const count of Object.values(current)) {
  currentTotal += count;
}

for (const count of Object.values(baseline)) {
  baselineTotal += count;
}

for (const [filePath, count] of Object.entries(current)) {
  const allowed = baseline[filePath] ?? 0;
  if (count > allowed) {
    regressions.push({ filePath, count, allowed });
  }
}

console.log('[time-semantics] 本地时间增量防回归扫描');
console.log(`- 扫描源码：${files.length} 个`);
console.log(`- 当前存量：${currentTotal} 处`);
console.log(`- baseline：${baselineTotal} 处`);
console.log('- 阻止新增：DateTime.Now、DateTime.Today、DateTimeOffset.Now');

if (regressions.length > 0) {
  console.error(`- 结果：${regressions.length} 个文件超过 baseline。`);
  for (const regression of regressions) {
    console.error(`  - ${regression.filePath}: ${regression.allowed} -> ${regression.count}`);
  }
  console.error('- 请改用注入的 TimeProvider / BusinessCalendar；治理存量后再显式刷新 baseline。');
  process.exit(1);
}

console.log('- 结果：通过。');
