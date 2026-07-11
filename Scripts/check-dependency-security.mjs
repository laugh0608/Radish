import process from 'node:process';

import { formatCommand, runCommand } from './process-runner.mjs';

const repoRoot = process.cwd();
const blockedSeverities = new Set(['high', 'critical']);

function readJson(label, value) {
  const normalized = String(value ?? '').trim();
  if (!normalized) {
    throw new Error(`${label} 未输出 JSON。`);
  }

  try {
    return JSON.parse(normalized);
  } catch (error) {
    throw new Error(`${label} JSON 无法解析：${error.message}`);
  }
}

function runAuditCommand(label, command, args) {
  console.log(`[dependency-security] ${label}`);
  console.log(`> ${formatCommand(command, args)}`);

  const result = runCommand(command, args, {
    cwd: repoRoot,
    encoding: 'utf8',
    stdio: ['ignore', 'pipe', 'pipe'],
  });

  if (result.error) {
    throw new Error(`${label} 无法执行：${result.error.message}`);
  }

  return result;
}

function readNpmFindings(result) {
  const report = readJson('npm audit', result.stdout);
  if (report.error) {
    const message = report.error.summary ?? report.error.detail ?? JSON.stringify(report.error);
    throw new Error(`npm audit 未完成：${message}`);
  }

  if (!report.vulnerabilities || !report.metadata?.vulnerabilities) {
    throw new Error('npm audit JSON 缺少 vulnerabilities / metadata，不能判定为安全通过。');
  }

  if (result.status !== 0 && result.status !== 1) {
    const stderr = String(result.stderr ?? '').trim();
    throw new Error(`npm audit 异常退出 (${result.status})${stderr ? `：${stderr}` : '。'}`);
  }

  return Object.values(report.vulnerabilities)
    .filter((item) => blockedSeverities.has(String(item?.severity ?? '').toLowerCase()))
    .map((item) => ({
      name: item.name ?? '<unknown>',
      severity: String(item.severity).toLowerCase(),
      direct: item.isDirect === true,
      range: item.range ?? '<unknown>',
    }));
}

function readNuGetFindings(result) {
  if (result.status !== 0) {
    const stderr = String(result.stderr ?? '').trim();
    throw new Error(`NuGet 漏洞审计异常退出 (${result.status})${stderr ? `：${stderr}` : '。'}`);
  }

  const report = readJson('NuGet 漏洞审计', result.stdout);
  if (!Array.isArray(report.projects)) {
    throw new Error('NuGet 漏洞审计 JSON 缺少 projects，不能判定为安全通过。');
  }

  const findings = [];
  for (const project of report.projects) {
    for (const framework of project.frameworks ?? []) {
      const packages = [
        ...(framework.topLevelPackages ?? []),
        ...(framework.transitivePackages ?? []),
      ];

      for (const dependency of packages) {
        for (const vulnerability of dependency.vulnerabilities ?? []) {
          const severity = String(vulnerability.severity ?? '').toLowerCase();
          if (!blockedSeverities.has(severity)) {
            continue;
          }

          findings.push({
            project: project.path ?? '<unknown>',
            name: dependency.id ?? '<unknown>',
            version: dependency.resolvedVersion ?? '<unknown>',
            severity,
            advisoryUrl: vulnerability.advisoryurl ?? '<unknown>',
          });
        }
      }
    }
  }

  return findings;
}

function printNpmFindings(findings) {
  console.log(`- npm High / Critical：${findings.length}`);
  for (const finding of findings) {
    console.log(
      `  - ${finding.name}@${finding.range} (${finding.severity}, ${finding.direct ? 'direct' : 'transitive'})`
    );
  }
}

function printNuGetFindings(findings) {
  console.log(`- NuGet High / Critical：${findings.length}`);
  for (const finding of findings) {
    console.log(
      `  - ${finding.name}@${finding.version} (${finding.severity}) [${finding.project}] ${finding.advisoryUrl}`
    );
  }
}

try {
  const npmResult = runAuditCommand('npm 生产依赖审计', 'npm', [
    'audit',
    '--omit=dev',
    '--json',
  ]);
  const npmFindings = readNpmFindings(npmResult);
  printNpmFindings(npmFindings);

  const nuGetResult = runAuditCommand('NuGet 直接与传递依赖审计', 'dotnet', [
    'package',
    'list',
    '--project',
    'Radish.slnx',
    '--vulnerable',
    '--include-transitive',
    '--format',
    'json',
    '--no-restore',
  ]);
  const nuGetFindings = readNuGetFindings(nuGetResult);
  printNuGetFindings(nuGetFindings);

  if (npmFindings.length > 0 || nuGetFindings.length > 0) {
    console.error('[dependency-security] 校验失败：存在 High / Critical 已知漏洞。');
    process.exit(1);
  }

  console.log('[dependency-security] 校验通过：npm / NuGet 无 High / Critical 已知漏洞。');
} catch (error) {
  console.error(`[dependency-security] 校验失败：${error.message}`);
  process.exit(1);
}
