import process from 'node:process';

import { readFileSync } from 'node:fs';

import { collectIdentityImpactMatches, normalizePath } from './identity-impact-rules.mjs';

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

function renderDefault(matches, allFiles) {
  console.log('[identity-impact] 身份语义影响面判定');
  console.log(`- 输入文件：${allFiles.length} 个`);
  console.log(`- 命中文件：${matches.length} 个`);

  if (matches.length === 0) {
    console.log('- 结果：未命中身份语义影响面。');
    return;
  }

  console.log('- 结果：命中身份语义影响面。');
  for (const match of matches) {
    console.log(`  - ${match}`);
  }
}

function renderGithubOutput(matches) {
  console.log(`impacted=${matches.length > 0 ? 'true' : 'false'}`);
  console.log(`matched_count=${matches.length}`);
  console.log('matched_files<<EOF');
  for (const match of matches) {
    console.log(match);
  }
  console.log('EOF');
}

function main() {
  const files = parseFiles().map(normalizePath);
  const matches = collectIdentityImpactMatches(files);
  const format = args.has('--format=github-output') ? 'github-output' : 'default';

  if (format === 'github-output') {
    renderGithubOutput(matches);
    return;
  }

  renderDefault(matches, files);
}

main();
