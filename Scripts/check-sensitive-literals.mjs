import { existsSync, readFileSync } from 'node:fs';
import path from 'node:path';
import process from 'node:process';

import { runCommand } from './process-runner.mjs';
import { findSensitiveLiteralMatches } from './sensitive-literal-rules.mjs';

const repoRoot = process.cwd();
const decoder = new TextDecoder('utf-8', { fatal: true });
const binaryExtensions = new Set([
  '.7z',
  '.bmp',
  '.db',
  '.dll',
  '.dylib',
  '.eot',
  '.exe',
  '.gif',
  '.gz',
  '.ico',
  '.jar',
  '.jpeg',
  '.jpg',
  '.pdb',
  '.pdf',
  '.pfx',
  '.png',
  '.snk',
  '.so',
  '.sqlite',
  '.tar',
  '.tgz',
  '.ttf',
  '.woff',
  '.woff2',
  '.zip',
]);

function collectCandidateFiles() {
  const result = runCommand(
    'git',
    ['ls-files', '-co', '--exclude-standard', '-z'],
    {
      cwd: repoRoot,
      encoding: null,
      stdio: ['ignore', 'pipe', 'inherit'],
    }
  );

  if (result.error) {
    console.error(`[sensitive-literals] 文件收集失败：${result.error.message}`);
    process.exit(1);
  }

  if (result.status !== 0) {
    process.exit(result.status ?? 1);
  }

  return (result.stdout ?? Buffer.alloc(0))
    .toString('utf8')
    .split('\0')
    .map((file) => file.trim())
    .filter(Boolean);
}

function hasBinaryControlBytes(buffer) {
  const sampleLength = Math.min(buffer.length, 8192);

  for (let index = 0; index < sampleLength; index += 1) {
    const byte = buffer[index];
    if (byte === 0) {
      return true;
    }
  }

  return false;
}

function readTextFile(filePath) {
  const absolutePath = path.join(repoRoot, filePath);
  if (!existsSync(absolutePath)) {
    return null;
  }

  if (binaryExtensions.has(path.extname(filePath).toLowerCase())) {
    return null;
  }

  const buffer = readFileSync(absolutePath);
  if (hasBinaryControlBytes(buffer)) {
    return null;
  }

  try {
    return decoder.decode(buffer);
  } catch {
    return null;
  }
}

const candidateFiles = collectCandidateFiles();
const matches = [];
let scannedFiles = 0;

for (const filePath of candidateFiles) {
  const content = readTextFile(filePath);
  if (content === null) {
    continue;
  }

  scannedFiles += 1;
  matches.push(...findSensitiveLiteralMatches(filePath, content));
}

console.log('[sensitive-literals] 高置信敏感字面量扫描');
console.log(`- 候选文件：${candidateFiles.length} 个`);
console.log(`- 文本文件：${scannedFiles} 个`);
console.log('- 规则：完整 JWT、硬编码 Bearer Token、私钥 PEM 头');

if (matches.length > 0) {
  console.error(`- 结果：发现 ${matches.length} 处阻断项。`);
  for (const match of matches) {
    console.error(
      `  - ${match.filePath}:${match.lineNumber} [${match.ruleId}] ${match.description}`
    );
  }
  console.error('- 不输出命中值；请删除字面量并改用环境变量或受控秘密存储。');
  process.exit(1);
}

console.log('- 结果：未发现高置信敏感字面量。');
