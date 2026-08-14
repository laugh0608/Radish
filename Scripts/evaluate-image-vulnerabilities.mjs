import fs from 'node:fs';
import path from 'node:path';
import process from 'node:process';
import { fileURLToPath } from 'node:url';

const CVE_ID_PATTERN = /^CVE-\d{4}-\d{4,}$/u;
const ISO_DATE_PATTERN = /^\d{4}-\d{2}-\d{2}$/u;
const EXCEPTION_SCHEMA_VERSION = 1;
const EVALUATION_SCHEMA_VERSION = 1;
const MAX_SUMMARY_FINDINGS = 20;
const SUPPORTED_IMAGE_NAMES = new Set([
  'radish-dbmigrate',
  'radish-api',
  'radish-auth',
  'radish-gateway',
  'radish-frontend',
]);

function normalizeRequiredString(value, fieldName, issues, entryLabel) {
  if (typeof value !== 'string' || value.trim().length === 0) {
    issues.push(`${entryLabel}.${fieldName} 必须是非空字符串。`);
    return '';
  }

  return value.trim();
}

function isValidIsoDate(value) {
  if (!ISO_DATE_PATTERN.test(value)) {
    return false;
  }

  const parsed = new Date(`${value}T00:00:00.000Z`);
  return !Number.isNaN(parsed.getTime()) && parsed.toISOString().slice(0, 10) === value;
}

function exceptionKey(entry) {
  return `${entry.image}\u0000${entry.packageName}\u0000${entry.vulnerabilityId}`;
}

function findingKey(finding) {
  return `${finding.image}\u0000${finding.packageName}\u0000${finding.vulnerabilityId}`;
}

export function validateExceptionRegister(register) {
  if (!register || typeof register !== 'object' || Array.isArray(register)) {
    throw new Error('镜像漏洞例外清单必须是 JSON 对象。');
  }

  const issues = [];
  if (register.schemaVersion !== EXCEPTION_SCHEMA_VERSION) {
    issues.push(`schemaVersion 必须为 ${EXCEPTION_SCHEMA_VERSION}。`);
  }

  if (!Array.isArray(register.exceptions)) {
    issues.push('exceptions 必须是数组。');
  }

  if (issues.length > 0) {
    throw new Error(`镜像漏洞例外清单无效：\n- ${issues.join('\n- ')}`);
  }

  const normalizedEntries = register.exceptions.map((entry, index) => {
    const entryLabel = `exceptions[${index}]`;
    if (!entry || typeof entry !== 'object' || Array.isArray(entry)) {
      issues.push(`${entryLabel} 必须是对象。`);
      return {
        vulnerabilityId: '',
        image: '',
        packageName: '',
        baseImage: '',
        reachability: '',
        reason: '',
        expiresOn: '',
      };
    }

    const vulnerabilityId = normalizeRequiredString(
      entry.vulnerabilityId,
      'vulnerabilityId',
      issues,
      entryLabel,
    ).toUpperCase();
    const image = normalizeRequiredString(entry.image, 'image', issues, entryLabel);
    const packageName = normalizeRequiredString(
      entry.packageName,
      'packageName',
      issues,
      entryLabel,
    );
    const baseImage = normalizeRequiredString(
      entry.baseImage,
      'baseImage',
      issues,
      entryLabel,
    );
    const reachability = normalizeRequiredString(
      entry.reachability,
      'reachability',
      issues,
      entryLabel,
    );
    const reason = normalizeRequiredString(entry.reason, 'reason', issues, entryLabel);
    const expiresOn = normalizeRequiredString(
      entry.expiresOn,
      'expiresOn',
      issues,
      entryLabel,
    );

    if (vulnerabilityId && !CVE_ID_PATTERN.test(vulnerabilityId)) {
      issues.push(
        `${entryLabel}.vulnerabilityId 必须是精确 CVE，不能使用通配符或宽泛规则。`,
      );
    }
    if (image && !SUPPORTED_IMAGE_NAMES.has(image)) {
      issues.push(`${entryLabel}.image 必须是五个正式 Radish 镜像之一。`);
    }
    if (packageName.includes('*') || packageName.includes('?')) {
      issues.push(`${entryLabel}.packageName 不能包含通配符。`);
    }
    if (expiresOn && !isValidIsoDate(expiresOn)) {
      issues.push(`${entryLabel}.expiresOn 必须是有效的 YYYY-MM-DD 日期。`);
    }

    return {
      vulnerabilityId,
      image,
      packageName,
      baseImage,
      reachability,
      reason,
      expiresOn,
    };
  });

  const seenKeys = new Set();
  for (const entry of normalizedEntries) {
    const key = exceptionKey(entry);
    if (seenKeys.has(key)) {
      issues.push(
        `例外重复：${entry.image} / ${entry.packageName} / ${entry.vulnerabilityId}。`,
      );
    }
    seenKeys.add(key);
  }

  if (issues.length > 0) {
    throw new Error(`镜像漏洞例外清单无效：\n- ${issues.join('\n- ')}`);
  }

  return normalizedEntries;
}

function normalizeFinding(image, result, vulnerability) {
  if (!vulnerability || typeof vulnerability !== 'object' || Array.isArray(vulnerability)) {
    throw new Error('Trivy report 中存在非对象 vulnerability。');
  }

  const vulnerabilityId = String(vulnerability.VulnerabilityID ?? '').trim().toUpperCase();
  const severity = String(vulnerability.Severity ?? '').trim().toUpperCase();
  if (!vulnerabilityId) {
    throw new Error('Trivy vulnerability 缺少 VulnerabilityID，不能安全判定。');
  }
  if (!severity) {
    throw new Error(`Trivy vulnerability ${vulnerabilityId} 缺少 Severity，不能安全判定。`);
  }

  return {
    image,
    vulnerabilityId,
    severity,
    packageName: String(vulnerability.PkgName ?? '<unknown>').trim() || '<unknown>',
    installedVersion:
      String(vulnerability.InstalledVersion ?? '<unknown>').trim() || '<unknown>',
    fixedVersion: String(vulnerability.FixedVersion ?? '').trim(),
    title: String(vulnerability.Title ?? '').trim(),
    primaryUrl: String(vulnerability.PrimaryURL ?? '').trim(),
    target: String(result.Target ?? '<unknown>').trim() || '<unknown>',
    class: String(result.Class ?? '').trim(),
    type: String(result.Type ?? '').trim(),
  };
}

export function extractTrivyFindings(report, image) {
  if (!report || typeof report !== 'object' || Array.isArray(report)) {
    throw new Error('Trivy report 必须是 JSON 对象。');
  }
  if (report.SchemaVersion !== 2) {
    throw new Error('Trivy report.SchemaVersion 必须为 2，不能对未知报告格式放行。');
  }
  if (!Array.isArray(report.Results)) {
    throw new Error('Trivy report.Results 必须是数组。');
  }

  const findings = [];
  for (const result of report.Results) {
    if (!result || typeof result !== 'object' || Array.isArray(result)) {
      throw new Error('Trivy report.Results 中存在非对象结果。');
    }
    if (result.Vulnerabilities != null && !Array.isArray(result.Vulnerabilities)) {
      throw new Error('Trivy result.Vulnerabilities 必须是数组或 null。');
    }

    for (const vulnerability of result.Vulnerabilities ?? []) {
      findings.push(normalizeFinding(image, result, vulnerability));
    }
  }

  return findings;
}

export function evaluateImageVulnerabilities({
  report,
  exceptionRegister,
  image,
  asOf = new Date().toISOString().slice(0, 10),
  evaluatedAtUtc = new Date().toISOString(),
}) {
  if (typeof image !== 'string' || image.trim().length === 0) {
    throw new Error('image 必须是非空字符串。');
  }
  if (!isValidIsoDate(asOf)) {
    throw new Error('asOf 必须是有效的 YYYY-MM-DD 日期。');
  }

  const normalizedImage = image.trim();
  if (!SUPPORTED_IMAGE_NAMES.has(normalizedImage)) {
    throw new Error(`不支持的正式镜像：${normalizedImage}。`);
  }
  const exceptions = validateExceptionRegister(exceptionRegister);
  const findings = extractTrivyFindings(report, normalizedImage);
  const relevantFindings = findings.filter(
    (finding) => finding.severity === 'HIGH' || finding.severity === 'CRITICAL',
  );
  const imageExceptions = exceptions.filter((entry) => entry.image === normalizedImage);
  const activeExceptions = new Map();
  const exceptionIssues = [];

  for (const entry of imageExceptions) {
    if (entry.expiresOn < asOf) {
      exceptionIssues.push({
        type: 'expired',
        vulnerabilityId: entry.vulnerabilityId,
        packageName: entry.packageName,
        message: `例外已于 ${entry.expiresOn} 过期，必须重新评估或删除。`,
      });
      continue;
    }
    activeExceptions.set(exceptionKey(entry), entry);
  }

  const blockingFindings = [];
  const reportedFindings = [];
  const acceptedFindings = [];
  const matchedExceptionKeys = new Set();

  for (const finding of relevantFindings) {
    const isBlockingByDefault =
      finding.severity === 'CRITICAL'
      || (finding.severity === 'HIGH' && finding.fixedVersion.length > 0);

    if (!isBlockingByDefault) {
      reportedFindings.push(finding);
      continue;
    }

    const key = findingKey(finding);
    const matchedException = activeExceptions.get(key);
    if (matchedException) {
      matchedExceptionKeys.add(key);
      acceptedFindings.push({
        ...finding,
        exception: matchedException,
      });
      continue;
    }

    blockingFindings.push(finding);
  }

  for (const [key, entry] of activeExceptions) {
    if (matchedExceptionKeys.has(key)) {
      continue;
    }

    exceptionIssues.push({
      type: 'unused',
      vulnerabilityId: entry.vulnerabilityId,
      packageName: entry.packageName,
      message:
        '例外没有匹配当前默认阻断项；漏洞可能已修复、已降为无修复 HIGH，或清单字段已漂移。',
    });
  }

  const status =
    blockingFindings.length > 0 || exceptionIssues.length > 0 ? 'blocked' : 'passed';
  const criticalCount = relevantFindings.filter(
    (finding) => finding.severity === 'CRITICAL',
  ).length;
  const fixedHighCount = relevantFindings.filter(
    (finding) => finding.severity === 'HIGH' && finding.fixedVersion.length > 0,
  ).length;
  const unfixedHighCount = relevantFindings.filter(
    (finding) => finding.severity === 'HIGH' && finding.fixedVersion.length === 0,
  ).length;

  return {
    schemaVersion: EVALUATION_SCHEMA_VERSION,
    image: normalizedImage,
    evaluatedAtUtc,
    evaluatedOn: asOf,
    status,
    counts: {
      critical: criticalCount,
      fixedHigh: fixedHighCount,
      unfixedHigh: unfixedHighCount,
      blocking: blockingFindings.length,
      acceptedByException: acceptedFindings.length,
      exceptionIssues: exceptionIssues.length,
    },
    blockingFindings,
    reportedFindings,
    acceptedFindings,
    exceptionIssues,
  };
}

function escapeMarkdownCell(value) {
  return String(value ?? '')
    .replaceAll('|', '\\|')
    .replaceAll('\r', ' ')
    .replaceAll('\n', ' ');
}

function buildFindingTable(findings, { includeException = false } = {}) {
  const headers = includeException
    ? '| Severity | CVE | Package | Installed | Fixed | Target | Exception expiry |'
    : '| Severity | CVE | Package | Installed | Fixed | Target |';
  const divider = includeException
    ? '| --- | --- | --- | --- | --- | --- | --- |'
    : '| --- | --- | --- | --- | --- | --- |';
  const rows = findings.slice(0, MAX_SUMMARY_FINDINGS).map((finding) => {
    const cells = [
      finding.severity,
      finding.vulnerabilityId,
      finding.packageName,
      finding.installedVersion,
      finding.fixedVersion || 'unfixed',
      finding.target,
    ];
    if (includeException) {
      cells.push(finding.exception?.expiresOn ?? '');
    }
    return `| ${cells.map(escapeMarkdownCell).join(' | ')} |`;
  });

  if (findings.length > MAX_SUMMARY_FINDINGS) {
    rows.push(
      `\n仅展示前 ${MAX_SUMMARY_FINDINGS} 项；完整结果见上传的 evaluation JSON。`,
    );
  }

  return [headers, divider, ...rows].join('\n');
}

export function buildMarkdownSummary(evaluation) {
  const resultLabel = evaluation.status === 'passed' ? 'PASS' : 'BLOCK';
  const lines = [
    `## Image vulnerability gate: \`${evaluation.image}\``,
    '',
    `- Result: **${resultLabel}**`,
    `- Critical: ${evaluation.counts.critical}`,
    `- HIGH with fix: ${evaluation.counts.fixedHigh}`,
    `- HIGH without fix (report only): ${evaluation.counts.unfixedHigh}`,
    `- Accepted by active exception: ${evaluation.counts.acceptedByException}`,
    `- Exception issues: ${evaluation.counts.exceptionIssues}`,
  ];

  if (evaluation.blockingFindings.length > 0) {
    lines.push('', '### Blocking findings', '', buildFindingTable(evaluation.blockingFindings));
  }
  if (evaluation.reportedFindings.length > 0) {
    lines.push(
      '',
      '### Unfixed HIGH findings (non-blocking)',
      '',
      buildFindingTable(evaluation.reportedFindings),
    );
  }
  if (evaluation.acceptedFindings.length > 0) {
    lines.push(
      '',
      '### Accepted exceptions',
      '',
      buildFindingTable(evaluation.acceptedFindings, { includeException: true }),
    );
  }
  if (evaluation.exceptionIssues.length > 0) {
    lines.push('', '### Exception register issues', '');
    for (const issue of evaluation.exceptionIssues) {
      lines.push(
        `- \`${issue.vulnerabilityId}\` / \`${issue.packageName}\`: ${issue.message}`,
      );
    }
  }

  return `${lines.join('\n')}\n`;
}

function parseCliArgs(rawArgs) {
  const options = {};
  const valueFlags = new Set([
    '--report',
    '--image',
    '--exceptions',
    '--output',
    '--summary',
    '--as-of',
  ]);

  for (let index = 0; index < rawArgs.length; index += 1) {
    const arg = rawArgs[index];
    if (arg === '--help' || arg === '-h') {
      options.help = true;
      continue;
    }
    if (!valueFlags.has(arg)) {
      throw new Error(`未知参数：${arg}`);
    }
    if (index + 1 >= rawArgs.length || rawArgs[index + 1].startsWith('--')) {
      throw new Error(`${arg} 缺少参数值。`);
    }
    options[arg.slice(2)] = rawArgs[index + 1];
    index += 1;
  }

  return options;
}

function readJson(filePath, label) {
  try {
    return JSON.parse(fs.readFileSync(filePath, 'utf8'));
  } catch (error) {
    throw new Error(`${label}读取失败（${filePath}）：${error.message}`);
  }
}

function writeUtf8(filePath, contents, { append = false } = {}) {
  fs.mkdirSync(path.dirname(path.resolve(filePath)), { recursive: true });
  if (append) {
    fs.appendFileSync(filePath, contents, 'utf8');
    return;
  }
  fs.writeFileSync(filePath, contents, 'utf8');
}

function printUsage() {
  console.log(
    '用法: node Scripts/evaluate-image-vulnerabilities.mjs --report <trivy.json> --image <name> [--exceptions <path>] [--output <evaluation.json>] [--summary <summary.md>] [--as-of YYYY-MM-DD]',
  );
}

function runCli() {
  const options = parseCliArgs(process.argv.slice(2));
  if (options.help) {
    printUsage();
    return;
  }
  if (!options.report || !options.image) {
    throw new Error('--report 与 --image 为必填参数。');
  }

  const reportPath = path.resolve(options.report);
  const exceptionPath = path.resolve(
    options.exceptions ?? '.github/image-vulnerability-exceptions.json',
  );
  const report = readJson(reportPath, 'Trivy report ');
  const exceptionRegister = readJson(exceptionPath, '镜像漏洞例外清单');
  const evaluation = evaluateImageVulnerabilities({
    report,
    exceptionRegister,
    image: options.image,
    asOf: options['as-of'],
  });
  const markdownSummary = buildMarkdownSummary(evaluation);

  if (options.output) {
    writeUtf8(options.output, `${JSON.stringify(evaluation, null, 2)}\n`);
  }
  if (options.summary) {
    writeUtf8(options.summary, markdownSummary, { append: true });
  }

  console.log(markdownSummary);
  if (evaluation.status === 'blocked') {
    process.exitCode = 1;
  }
}

const currentFilePath = fileURLToPath(import.meta.url);
if (process.argv[1] && path.resolve(process.argv[1]) === currentFilePath) {
  try {
    runCli();
  } catch (error) {
    console.error(`[image-vulnerability] ${error.message}`);
    process.exitCode = 2;
  }
}
