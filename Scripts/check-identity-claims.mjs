import { readdirSync, readFileSync } from 'node:fs';
import { dirname, extname, join, relative, resolve, sep } from 'node:path';
import process from 'node:process';
import { fileURLToPath } from 'node:url';

const scriptDir = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(scriptDir, '..');

const scanRoots = [
  'Radish.Api',
  'Radish.Auth',
  'Radish.Common',
  'Radish.Extension',
  'Radish.Gateway',
  'Radish.Infrastructure',
  'Radish.Repository',
  'Radish.Service',
];

const excludedFiles = new Set([
  'Radish.Api/Program.cs',
  'Radish.Auth/Controllers/AccountController.cs',
  'Radish.Auth/Controllers/AuthorizationController.cs',
  'Radish.Auth/Controllers/UserInfoController.cs',
  'Radish.Auth/Program.cs',
]);

const excludedPrefixes = [
  'Radish.Common/HttpContextTool/',
];

const rules = [
  {
    id: 'raw-claim-lookup',
    description: '直接调用 FindFirst/FindAll 读取 Claim',
    test: (line) => /\.(FindFirst|FindAll)\s*\(/.test(line),
  },
  {
    id: 'claim-types',
    description: '直接引用 ClaimTypes 常量',
    test: (line) => /\bClaimTypes\./.test(line),
  },
  {
    id: 'principal-role-check',
    description: '直接通过 ClaimsPrincipal / HttpContext.User 做角色判断',
    test: (line) => /\b(?:User|principal|Principal|context\.User|httpContext\.User)\.IsInRole\s*\(/.test(line),
  },
  {
    id: 'raw-claim-literal',
    description: '直接写入协议 Claim 字符串字面量',
    test: (line) => /"(sub|jti|tenant_id|TenantId|role|scope)"/.test(line),
  },
];

function toRepoPath(filePath) {
  return relative(repoRoot, filePath).split(sep).join('/');
}

function shouldSkipFile(repoPath) {
  if (excludedFiles.has(repoPath)) {
    return true;
  }

  return excludedPrefixes.some((prefix) => repoPath.startsWith(prefix));
}

function getAllCsFiles(rootPath) {
  const files = [];
  const stack = [rootPath];

  while (stack.length > 0) {
    const current = stack.pop();
    if (!current) {
      continue;
    }

    for (const entry of readdirSync(current, { withFileTypes: true })) {
      const fullPath = join(current, entry.name);
      if (entry.isDirectory()) {
        stack.push(fullPath);
        continue;
      }

      if (extname(entry.name) === '.cs') {
        files.push(fullPath);
      }
    }
  }

  return files.sort();
}

function shouldSkipLine(line) {
  const trimmed = line.trim();
  if (!trimmed) {
    return true;
  }

  if (trimmed.startsWith('//')) {
    return true;
  }

  if (trimmed.includes('identity-scan: ignore')) {
    return true;
  }

  return false;
}

function collectFindings(filePath) {
  const repoPath = toRepoPath(filePath);
  if (shouldSkipFile(repoPath)) {
    return [];
  }

  const lines = readFileSync(filePath, 'utf8').split(/\r?\n/);
  const findings = [];

  for (let index = 0; index < lines.length; index += 1) {
    const line = lines[index];
    if (shouldSkipLine(line)) {
      continue;
    }

    for (const rule of rules) {
      if (!rule.test(line)) {
        continue;
      }

      findings.push({
        file: repoPath,
        line: index + 1,
        rule: rule.id,
        description: rule.description,
        content: line.trim(),
      });
    }
  }

  return findings;
}

const scanFiles = scanRoots
  .map((root) => join(repoRoot, root))
  .flatMap((rootPath) => getAllCsFiles(rootPath));

const scannedFiles = scanFiles.filter((filePath) => !shouldSkipFile(toRepoPath(filePath)));
const findings = scanFiles.flatMap((filePath) => collectFindings(filePath));

console.log('[Identity Claim 扫描]');
console.log(`- 扫描目录：${scanRoots.join(', ')}`);
console.log(`- 扫描文件：${scannedFiles.length} 个`);
console.log(`- 排除文件/目录：${excludedFiles.size + excludedPrefixes.length} 条`);

if (findings.length === 0) {
  console.log('- 结果：未发现新的身份语义回归命中。');
  process.exit(0);
}

console.error(`- 结果：发现 ${findings.length} 条疑似回归命中。`);
for (const finding of findings) {
  console.error(`  - [${finding.rule}] ${finding.file}:${finding.line} ${finding.description}`);
  console.error(`    ${finding.content}`);
}

process.exit(1);
