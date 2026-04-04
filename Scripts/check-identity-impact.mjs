import process from 'node:process';

import { readFileSync } from 'node:fs';

import {
  collectIdentityImpactDetails,
  collectIdentityImpactReasonGroups,
  normalizePath,
} from './identity-impact-rules.mjs';

const args = new Set(process.argv.slice(2));

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

function renderDefault(details, reasonGroups, allFiles) {
  console.log('[identity-impact] 身份语义影响面判定');
  console.log(`- 输入文件：${allFiles.length} 个`);
  console.log(`- 命中文件：${details.length} 个`);
  console.log(`- 命中原因：${reasonGroups.length} 类`);

  if (details.length === 0) {
    console.log('- 结果：未命中身份语义影响面。');
    return;
  }

  console.log('- 结果：命中身份语义影响面。');
  for (const reasonGroup of reasonGroups) {
    console.log(`  - ${reasonGroup.label}：${reasonGroup.files.length} 个文件`);
  }

  console.log('- 命中文件明细：');
  for (const detail of details) {
    const labels = detail.reasons.map((reason) => reason.label).join(' / ');
    console.log(`  - ${detail.file} [${labels}]`);
  }
}

function renderGithubOutput(details, reasonGroups) {
  console.log(`impacted=${details.length > 0 ? 'true' : 'false'}`);
  console.log(`matched_count=${details.length}`);
  console.log('matched_files<<EOF');
  for (const detail of details) {
    console.log(detail.file);
  }
  console.log('EOF');
  console.log(`matched_reason_count=${reasonGroups.length}`);
  console.log('matched_reason_keys<<EOF');
  for (const reasonGroup of reasonGroups) {
    console.log(reasonGroup.key);
  }
  console.log('EOF');
  console.log('matched_reason_labels<<EOF');
  for (const reasonGroup of reasonGroups) {
    console.log(reasonGroup.label);
  }
  console.log('EOF');
}

function main() {
  const files = parseFiles().map(normalizePath);
  const details = collectIdentityImpactDetails(files);
  const reasonGroups = collectIdentityImpactReasonGroups(files);
  const format = args.has('--format=github-output') ? 'github-output' : 'default';

  if (format === 'github-output') {
    renderGithubOutput(details, reasonGroups);
    return;
  }

  renderDefault(details, reasonGroups, files);
}

main();
