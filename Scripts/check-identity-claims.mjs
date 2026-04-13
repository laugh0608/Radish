import { readdirSync, readFileSync } from 'node:fs';
import { dirname, extname, join, relative, resolve, sep } from 'node:path';
import process from 'node:process';
import { fileURLToPath } from 'node:url';

const args = new Set(process.argv.slice(2));
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

const protocolOutputGuards = [
  {
    file: 'Radish.Auth/Controllers/AccountController.cs',
    guards: [
      {
        type: 'forbidden',
        id: 'legacy-nameidentifier-output',
        description: 'Phase 4 后禁止重新输出历史 NameIdentifier Claim',
        test: /new\s*\(\s*ClaimTypes\.NameIdentifier\b|new\s+Claim\s*\(\s*ClaimTypes\.NameIdentifier\b|new\s*\(\s*UserClaimTypes\.LegacyNameIdentifier\b|new\s+Claim\s*\(\s*UserClaimTypes\.LegacyNameIdentifier\b/,
      },
      {
        type: 'forbidden',
        id: 'legacy-name-output',
        description: 'Phase 4 后禁止重新输出历史 Name Claim',
        test: /new\s*\(\s*ClaimTypes\.Name\b|new\s+Claim\s*\(\s*ClaimTypes\.Name\b|new\s*\(\s*UserClaimTypes\.LegacyName\b|new\s+Claim\s*\(\s*UserClaimTypes\.LegacyName\b/,
      },
      {
        type: 'forbidden',
        id: 'legacy-role-output',
        description: 'Phase 4 后禁止重新输出历史 Role Claim',
        test: /new\s*\(\s*ClaimTypes\.Role\b|new\s+Claim\s*\(\s*ClaimTypes\.Role\b|new\s*\(\s*UserClaimTypes\.LegacyRole\b|new\s+Claim\s*\(\s*UserClaimTypes\.LegacyRole\b/,
      },
      {
        type: 'forbidden',
        id: 'legacy-tenant-output',
        description: 'Phase 4 后禁止重新输出历史 TenantId Claim',
        test: /new\s*\(\s*"TenantId"\b|new\s+Claim\s*\(\s*"TenantId"\b|new\s*\(\s*UserClaimTypes\.LegacyTenantId\b|new\s+Claim\s*\(\s*UserClaimTypes\.LegacyTenantId\b/,
      },
      {
        type: 'forbidden',
        id: 'legacy-jti-output',
        description: 'Phase 4 后禁止把 jti 当成用户身份输出承诺',
        test: /new\s*\(\s*JwtRegisteredClaimNames\.Jti\b|new\s+Claim\s*\(\s*JwtRegisteredClaimNames\.Jti\b|new\s*\(\s*UserClaimTypes\.LegacyJti\b|new\s+Claim\s*\(\s*UserClaimTypes\.LegacyJti\b/,
      },
    ],
  },
  {
    file: 'Radish.Auth/Controllers/AuthorizationController.cs',
    guards: [
      {
        type: 'required',
        id: 'legacy-destination-break',
        description: 'Phase 4 后 AuthorizationController 必须继续阻止历史 Claim 写入 Token destinations',
        test: /if\s*\(\s*claim\.Type\s*==\s*UserClaimTypes\.LegacyNameIdentifier[\s\S]*?claim\.Type\s*==\s*UserClaimTypes\.LegacyJti[\s\S]*?\)\s*\{\s*yield break;\s*\}/,
      },
    ],
  },
  {
    file: 'Radish.Auth/Controllers/UserInfoController.cs',
    guards: [
      {
        type: 'forbidden',
        id: 'userinfo-legacy-output',
        description: 'userinfo 对外结构不得回退为历史 Claim 键',
        test: /\[\s*UserClaimTypes\.LegacyTenantId\s*\]|\[\s*UserClaimTypes\.LegacyRole\s*\]|\[\s*UserClaimTypes\.LegacyName\s*\]|\[\s*UserClaimTypes\.LegacyNameIdentifier\s*\]|\[\s*ClaimTypes\.Role\s*\]/,
      },
    ],
  },
];

function hasFlag(flag) {
  return args.has(flag);
}

function printUsage() {
  console.log('用法: node Scripts/check-identity-claims.mjs [--runtime-only] [--protocol-only]');
  console.log('');
  console.log('--runtime-only   只扫描运行时代码中的散点 Claim 读取回归');
  console.log('--protocol-only  只扫描 Radish.Auth 协议输出侧的回退风险');
}

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

function findLineNumber(content, matchIndex) {
  if (matchIndex <= 0) {
    return 1;
  }

  return content.slice(0, matchIndex).split(/\r?\n/).length;
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

function collectProtocolGuardFindings(filePath) {
  const repoPath = toRepoPath(filePath);
  const guardDefinition = protocolOutputGuards.find((item) => item.file === repoPath);
  if (!guardDefinition) {
    return [];
  }

  const content = readFileSync(filePath, 'utf8');
  const findings = [];

  for (const guard of guardDefinition.guards) {
    const match = guard.test.exec(content);

    if (guard.type === 'required') {
      if (match) {
        continue;
      }

      findings.push({
        file: repoPath,
        line: 1,
        rule: guard.id,
        description: guard.description,
        content: '未匹配到预期的协议输出保护片段',
      });
      continue;
    }

    if (!match) {
      continue;
    }

    findings.push({
      file: repoPath,
      line: findLineNumber(content, match.index),
      rule: guard.id,
      description: guard.description,
      content: match[0].trim(),
    });
  }

  return findings;
}

if (hasFlag('--help') || hasFlag('-h')) {
  printUsage();
  process.exit(0);
}

const runtimeOnly = hasFlag('--runtime-only');
const protocolOnly = hasFlag('--protocol-only');

if (runtimeOnly && protocolOnly) {
  console.error('[Identity Claim 扫描] `--runtime-only` 与 `--protocol-only` 不能同时使用。');
  process.exit(1);
}

const shouldScanRuntime = !protocolOnly;
const shouldScanProtocol = !runtimeOnly;
const scanFiles = scanRoots
  .map((root) => join(repoRoot, root))
  .flatMap((rootPath) => getAllCsFiles(rootPath));

const scannedFiles = scanFiles.filter((filePath) => !shouldSkipFile(toRepoPath(filePath)));
const findings = scanFiles.flatMap((filePath) => {
  const fileFindings = [];

  if (shouldScanRuntime) {
    fileFindings.push(...collectFindings(filePath));
  }

  if (shouldScanProtocol) {
    fileFindings.push(...collectProtocolGuardFindings(filePath));
  }

  return fileFindings;
});

console.log('[Identity Claim 扫描]');
console.log(`- 扫描目录：${scanRoots.join(', ')}`);
console.log(`- 扫描文件：${scannedFiles.length} 个`);
console.log(`- 排除文件/目录：${excludedFiles.size + excludedPrefixes.length} 条`);
console.log(`- 扫描模式：${shouldScanRuntime && shouldScanProtocol ? 'runtime + protocol' : shouldScanRuntime ? 'runtime' : 'protocol'}`);
if (shouldScanProtocol) {
  console.log(`- 协议输出守卫：${protocolOutputGuards.length} 个文件`);
}

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
