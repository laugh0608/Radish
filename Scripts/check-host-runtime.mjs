import http from 'node:http';
import https from 'node:https';
import process from 'node:process';

import {
  buildSummaryActionReport,
  buildTriageSummaryLines,
  printSummaryActionReport,
  writeSummaryActionReport,
} from './m14-reporting.mjs';

const args = process.argv.slice(2);

function getArgValue(flagName, defaultValue) {
  const directPrefix = `${flagName}=`;
  const direct = args.find((arg) => arg.startsWith(directPrefix));
  if (direct) {
    return direct.slice(directPrefix.length);
  }

  const index = args.indexOf(flagName);
  if (index >= 0 && index + 1 < args.length) {
    return args[index + 1];
  }

  return defaultValue;
}

function hasFlag(flagName) {
  return args.includes(flagName);
}

function printUsage() {
  console.log('用法: node Scripts/check-host-runtime.mjs [options]');
  console.log('');
  console.log('默认检查以下运行态健康端点：');
  console.log('  - Gateway: https://localhost:5000/health');
  console.log('  - Api:     http://localhost:5100/health');
  console.log('  - Auth:    http://localhost:5200/health');
  console.log('');
  console.log('可选参数：');
  console.log('  --gateway <url>        覆盖 Gateway 健康检查地址');
  console.log('  --api <url>            覆盖 Api 健康检查地址');
  console.log('  --auth <url>           覆盖 Auth 健康检查地址');
  console.log('  --timeout-ms <number>  单个端点超时时间，默认 5000');
  console.log('  --details              当默认检查失败时，追加抓取失败宿主的 /healthz 明细摘要');
  console.log('  --report               输出可直接回写到记录/PR 的固定 Markdown 报告');
  console.log('  --report-file <path>   将 Markdown 报告直接写入指定文件（会自动启用 --report）');
  console.log('  --strict-tls           严格校验 HTTPS 证书（默认对 localhost/127.0.0.1 放宽）');
}

function shouldRelaxTls(url, strictTls) {
  if (strictTls) {
    return false;
  }

  return url.protocol === 'https:' &&
    (url.hostname === 'localhost' || url.hostname === '127.0.0.1');
}

function truncateText(text, maxLength = 180) {
  if (!text) {
    return '';
  }

  const normalized = text.replace(/\s+/g, ' ').trim();
  if (normalized.length <= maxLength) {
    return normalized;
  }

  return `${normalized.slice(0, maxLength - 3)}...`;
}

function inferFailureCategory(result) {
  const body = (result.body || '').toLowerCase();
  const errorCode = (result.errorCode || '').toLowerCase();

  if (
    errorCode === 'etimedout' ||
    body.includes('请求超时') ||
    body.includes('timeout') ||
    body.includes('timed out')
  ) {
    return {
      code: 'timeout',
      label: '请求超时',
      nextStep: '先看对应宿主是否卡在启动期，再看日志、下游转发或数据库连接是否阻塞。',
    };
  }

  if (
    errorCode === 'econnrefused' ||
    body.includes('econnrefused') ||
    body.includes('actively refused') ||
    body.includes('由于目标计算机积极拒绝') ||
    body.includes('connect failed')
  ) {
    return {
      code: 'connection-refused',
      label: '端口未监听/宿主未启动',
      nextStep: '先确认对应宿主进程是否已启动，以及监听地址和端口是否就是当前检查地址。',
    };
  }

  if (
    body.includes('self-signed certificate') ||
    body.includes('unable to verify') ||
    body.includes('certificate') ||
    body.includes('hostname/ip does not match')
  ) {
    return {
      code: 'tls-error',
      label: 'TLS/证书问题',
      nextStep: '先确认开发证书或反代证书是否正确；如果是本机 localhost 自签名，可对照 --strict-tls 再复核一次。',
    };
  }

  if (result.statusCode === 503 && body.includes('unhealthy')) {
    return {
      code: 'reported-unhealthy',
      label: '宿主已启动但健康检查未通过',
      nextStep: result.name === 'Gateway'
        ? '优先看 Gateway 日志和其下游健康项，确认是哪个下游把聚合健康打成失败或降级。'
        : '优先看该宿主日志、依赖连接和健康检查注册项，确认为什么自报 Unhealthy。',
    };
  }

  if (result.statusCode === 404) {
    return {
      code: 'endpoint-missing',
      label: '健康端点不存在',
      nextStep: '先确认当前健康检查 URL 是否正确，以及宿主是否真的暴露了该 /health 路径。',
    };
  }

  if (result.statusCode === 401 || result.statusCode === 403) {
    return {
      code: 'unexpected-auth',
      label: '健康端点被鉴权拦截',
      nextStep: '先确认 /health 是否应保持匿名可访问，避免把认证或网关规则错误地套到健康检查端点上。',
    };
  }

  if (result.statusCode >= 500) {
    return {
      code: 'http-5xx',
      label: '宿主返回 5xx',
      nextStep: '先看该宿主日志，再确认请求是否已经打到正确实例，以及依赖项是否在运行。',
    };
  }

  if (result.statusCode >= 400) {
    return {
      code: 'http-4xx',
      label: '宿主返回 4xx',
      nextStep: '先确认健康检查地址、反代规则和端点暴露路径是否匹配当前环境。',
    };
  }

  return {
    code: 'unknown',
    label: '未归类异常',
    nextStep: '先看该宿主日志和当前检查地址，再按 M14 清单回到 doctor/verify、健康检查和网关链路逐层排查。',
  };
}

function deriveHealthDetailsUrl(healthUrl) {
  const url = new URL(healthUrl);
  if (url.pathname.endsWith('/health')) {
    url.pathname = `${url.pathname.slice(0, -'/health'.length)}/healthz`;
  } else {
    url.pathname = '/healthz';
  }

  url.search = '';
  return url.toString();
}

function tryParseJson(text) {
  if (!text) {
    return null;
  }

  try {
    return JSON.parse(text);
  } catch {
    return null;
  }
}

function formatDurationMs(value) {
  return Number.isFinite(value) ? `${value.toFixed(2)}ms` : 'n/a';
}

function getHealthDetailsSummary(targetName, result) {
  if (!result.ok) {
    const category = inferFailureCategory(result);
    return {
      ok: false,
      summary: null,
      targetName,
      error: {
        statusCode: result.statusCode || 0,
        category,
        response: truncateText(result.body),
      },
    };
  }

  const payload = tryParseJson(result.body);
  if (!payload) {
    return {
      ok: false,
      summary: null,
      targetName,
      error: {
        statusCode: result.statusCode || 0,
        category: {
          code: 'invalid-json',
          label: 'healthz 响应不是可解析 JSON',
          nextStep: `先确认 ${targetName} /healthz 是否已切到结构化 JSON 输出，或是否被其他反代层改写了响应。`,
        },
        response: truncateText(result.body),
      },
    };
  }

  const entries = Array.isArray(payload.entries)
    ? payload.entries.map((entry) => ({
        name: entry.name ?? 'unknown',
        status: entry.status ?? 'unknown',
        tags: Array.isArray(entry.tags) ? entry.tags : [],
        durationMs: Number.isFinite(entry.durationMs) ? entry.durationMs : null,
        description: typeof entry.description === 'string' ? truncateText(entry.description, 120) : '',
        exception: typeof entry.exception === 'string' ? truncateText(entry.exception, 120) : '',
      }))
    : [];

  return {
    ok: true,
    error: null,
    targetName,
    summary: {
      overallStatus: payload.status ?? 'unknown',
      generatedAtUtc: payload.generatedAtUtc ?? 'n/a',
      totalDurationMs: Number.isFinite(payload.totalDurationMs) ? payload.totalDurationMs : null,
      entries,
    },
  };
}

function printHealthDetailsSummary(targetName, result) {
  const details = getHealthDetailsSummary(targetName, result);
  if (!details.ok) {
    console.error(`[host-runtime] ${targetName} /healthz 拉取失败 (${details.error.statusCode || 'no-response'})`);
    console.error(`[host-runtime]   分类: ${details.error.category.label} (${details.error.category.code})`);
    if (details.error.response) {
      console.error(`[host-runtime]   响应: ${details.error.response}`);
    }
    return details;
  }

  console.error(`[host-runtime] ${targetName} /healthz 明细：`);
  console.error(
    `[host-runtime]   overall=${details.summary.overallStatus} generatedAtUtc=${details.summary.generatedAtUtc} total=${formatDurationMs(details.summary.totalDurationMs)}`
  );

  if (details.summary.entries.length === 0) {
    console.error('[host-runtime]   entries: 无可用明细。');
    return details;
  }

  for (const entry of details.summary.entries) {
    const tags = entry.tags.length > 0
      ? entry.tags.join(', ')
      : 'none';
    const extras = [entry.description, entry.exception]
      .filter((item) => typeof item === 'string' && item.trim().length > 0);

    const suffix = extras.length > 0 ? ` | ${extras.join(' | ')}` : '';
    console.error(
      `[host-runtime]   - ${entry.name}: ${entry.status} [${tags}] (${formatDurationMs(entry.durationMs)})${suffix}`
    );
  }

  return details;
}

function buildResultReportLine(result) {
  if (result.ok) {
    return `- ${result.name}: 正常 (${result.statusCode})`;
  }

  const category = inferFailureCategory(result);
  const response = result.body ? ` | 响应: ${truncateText(result.body, 100)}` : '';
  return `- ${result.name}: 异常 (${result.statusCode || 'no-response'}) | 分类: ${category.label} (${category.code})${response}`;
}

function buildHealthDetailsReportLines(detailsByTarget) {
  if (!detailsByTarget || detailsByTarget.length === 0) {
    return [];
  }

  const lines = [];

  for (const details of detailsByTarget) {
    const label = `${details.targetName} /healthz`;
    if (!details.ok) {
      const response = details.error.response ? ` | 响应: ${details.error.response}` : '';
      lines.push(`- ${label}: 拉取失败 (${details.error.statusCode || 'no-response'}) | 分类: ${details.error.category.label} (${details.error.category.code})${response}`);
      continue;
    }

    lines.push(`- ${label}: ${details.summary.overallStatus} | generatedAtUtc: ${details.summary.generatedAtUtc} | total: ${formatDurationMs(details.summary.totalDurationMs)}`);

    for (const entry of details.summary.entries) {
      const tags = entry.tags.length > 0 ? entry.tags.join(', ') : 'none';
      const extras = [entry.description, entry.exception]
        .filter((item) => item);
      const suffix = extras.length > 0 ? ` | ${extras.join(' | ')}` : '';
      lines.push(`- ${label} entry: ${entry.name} | ${entry.status} | tags: ${tags} | duration: ${formatDurationMs(entry.durationMs)}${suffix}`);
    }
  }

  return lines;
}

function buildActionLines(results, detailsByTarget) {
  const lines = [];
  const detailedTargets = new Set((detailsByTarget || []).map((details) => details.targetName));

  for (const result of results) {
    if (result.ok) {
      continue;
    }

    if (detailedTargets.has(result.name)) {
      continue;
    }

    const category = inferFailureCategory(result);
    lines.push(`- ${result.name}: ${category.nextStep}`);
  }

  for (const details of detailsByTarget || []) {
    const label = `${details.targetName} /healthz`;

    if (!details.ok) {
      lines.push(`- ${label}: ${details.error.category.nextStep}`);
      continue;
    }

    const degradedEntries = details.summary.entries.filter(
      (entry) => entry.status !== 'Healthy'
    );

    for (const entry of degradedEntries) {
      const reason = [entry.description, entry.exception]
        .filter((item) => item)
        .join(' | ');
      const suffix = reason ? ` 当前摘要: ${reason}` : '';
      const isGatewayExtended = details.targetName === 'Gateway' && entry.tags.includes('extended');

      if (isGatewayExtended) {
        lines.push(`- ${label} ${entry.name}: 优先确认该扩展下游是否应在本轮启动；若不属于最小宿主链，可按 M14 口径单独记录为降级观测项。${suffix}`);
        continue;
      }

      lines.push(`- ${label} ${entry.name}: 优先结合当前宿主日志与健康检查注册项继续排查。${suffix}`);
    }
  }

  if (lines.length === 0) {
    lines.push('- 当前运行态健康检查通过，可继续进入更上层联调或保留为本轮复核记录。');
  }

  return [...new Set(lines)];
}

function getRuntimeTriageFindings(results, detailsByTarget) {
  const findings = results
    .filter((result) => !result.ok)
    .map((result) => {
      const category = inferFailureCategory(result);
      return {
        scope: result.name,
        code: category.code,
      };
    });

  for (const details of detailsByTarget || []) {
    if (details.ok) {
      for (const entry of details.summary.entries.filter((entry) => entry.status !== 'Healthy')) {
        findings.push({
          scope: `${details.targetName} /healthz`,
          code: `healthz-${String(entry.status).toLowerCase()}`,
        });
      }
      continue;
    }

    findings.push({
      scope: `${details.targetName} /healthz`,
      code: details.error.category.code,
    });
  }

  return findings;
}

function getRuntimeNextStage(results) {
  if (results.every((result) => result.ok)) {
    return 'continue-smoke-or-record';
  }

  return 'inspect-runtime-and-host-logs';
}

function buildMarkdownReport({ executedAtUtc, results, detailsByTarget, strictTls, timeoutMs }) {
  const overallStatus = results.every((result) => result.ok) ? 'passed' : 'failed';
  const summaryLines = [
    ...buildTriageSummaryLines({
      route: 'runtime',
      findings: getRuntimeTriageFindings(results, detailsByTarget),
      nextStage: getRuntimeNextStage(results),
    }),
    ...results.map((result) => buildResultReportLine(result)),
    ...buildHealthDetailsReportLines(detailsByTarget),
  ];
  const actionLines = buildActionLines(results, detailsByTarget);

  return buildSummaryActionReport({
    title: 'Host Runtime Check',
    summaryLines: [
      `- Time: ${executedAtUtc}`,
      `- Overall: ${overallStatus}`,
      `- TimeoutMs: ${timeoutMs}`,
      `- StrictTls: ${strictTls ? 'true' : 'false'}`,
      ...summaryLines,
    ],
    actionLines,
  });
}

function requestHealth(target, timeoutMs, strictTls) {
  const url = new URL(target.url);
  const transport = url.protocol === 'https:' ? https : http;

  return new Promise((resolve) => {
    const request = transport.request(
      url,
      {
        method: 'GET',
        timeout: timeoutMs,
        rejectUnauthorized: !shouldRelaxTls(url, strictTls),
      },
      (response) => {
        let body = '';
        response.setEncoding('utf8');
        response.on('data', (chunk) => {
          body += chunk;
        });

        response.on('end', () => {
          resolve({
            name: target.name,
            url: target.url,
            ok: (response.statusCode ?? 500) >= 200 && (response.statusCode ?? 500) < 300,
            statusCode: response.statusCode ?? 0,
            body: body.trim(),
          });
        });
      }
    );

    request.on('timeout', () => {
      request.destroy(new Error(`请求超时（>${timeoutMs}ms）`));
    });

    request.on('error', (error) => {
        resolve({
          name: target.name,
          url: target.url,
          ok: false,
          statusCode: 0,
          errorCode: error.code || '',
          body: error.message,
        });
      });

    request.end();
  });
}

if (hasFlag('--help') || hasFlag('-h')) {
  printUsage();
  process.exit(0);
}

const strictTls = hasFlag('--strict-tls');
const showDetails = hasFlag('--details');
const reportFile = getArgValue('--report-file', '');
const showReport = hasFlag('--report') || reportFile.length > 0;
const timeoutMs = Number.parseInt(getArgValue('--timeout-ms', '5000'), 10);

if (!Number.isFinite(timeoutMs) || timeoutMs <= 0) {
  console.error('[host-runtime] --timeout-ms 必须是大于 0 的整数。');
  process.exit(1);
}

const targets = [
  {
    name: 'Gateway',
    url: getArgValue('--gateway', 'https://localhost:5000/health'),
  },
  {
    name: 'Api',
    url: getArgValue('--api', 'http://localhost:5100/health'),
  },
  {
    name: 'Auth',
    url: getArgValue('--auth', 'http://localhost:5200/health'),
  },
];
const detailsTimeoutMs = Math.max(timeoutMs, 5000);

console.log('[host-runtime] 开始检查宿主运行态健康端点。');
if (!strictTls) {
  console.log('[host-runtime] 本地 https://localhost / 127.0.0.1 默认放宽证书校验；如需严格校验，请追加 --strict-tls。');
}

const results = [];
const executedAtUtc = new Date().toISOString();
for (const target of targets) {
  console.log(`\n[host-runtime] ${target.name}`);
  console.log(`> GET ${target.url}`);
  results.push(await requestHealth(target, timeoutMs, strictTls));
}

const failedResults = results.filter((item) => !item.ok);
const healthDetailsByTarget = [];

for (const result of results) {
  if (result.ok) {
    console.log(`[host-runtime] ✓ ${result.name} 正常 (${result.statusCode})`);
    continue;
  }

  console.error(`[host-runtime] ✗ ${result.name} 异常 (${result.statusCode || 'no-response'})`);
  const category = inferFailureCategory(result);
  console.error(`[host-runtime]   分类: ${category.label} (${category.code})`);
  if (result.body) {
    console.error(`[host-runtime]   响应: ${truncateText(result.body)}`);
  }
  console.error(`[host-runtime]   下一步: ${category.nextStep}`);
}

if (failedResults.length > 0) {
  if (showDetails) {
    for (const failedResult of failedResults) {
      const detailsTarget = {
        name: `${failedResult.name}Details`,
        url: deriveHealthDetailsUrl(failedResult.url),
      };

      console.error(`\n[host-runtime] 追加抓取 ${failedResult.name} 明细：`);
      console.error(`> GET ${detailsTarget.url}`);
      const detailsResult = await requestHealth(detailsTarget, detailsTimeoutMs, strictTls);
      healthDetailsByTarget.push(printHealthDetailsSummary(failedResult.name, detailsResult));
    }
  }

  if (showReport) {
    const markdownReport = buildMarkdownReport({
      executedAtUtc,
      results,
      detailsByTarget: healthDetailsByTarget,
      strictTls,
      timeoutMs,
    });
    printSummaryActionReport('host-runtime', markdownReport);
    if (reportFile) {
      await writeSummaryActionReport('host-runtime', reportFile, markdownReport);
    }
  }

  console.error('\n[host-runtime] 运行态健康检查未通过。');
  console.error('[host-runtime] 失败摘要：');
  for (const result of failedResults) {
    const category = inferFailureCategory(result);
    console.error(
      `[host-runtime] - ${result.name}: ${category.label} (${category.code})`
    );
  }
  console.error('[host-runtime] 建议顺序：先确认宿主是否已启动，再回到 M14 清单按 doctor/verify、日志、Gateway/反代链路继续排查。');
  process.exit(1);
}

if (showReport) {
  const markdownReport = buildMarkdownReport({
    executedAtUtc,
    results,
    detailsByTarget: healthDetailsByTarget,
    strictTls,
    timeoutMs,
  });
  printSummaryActionReport('host-runtime', markdownReport);
  if (reportFile) {
    await writeSummaryActionReport('host-runtime', reportFile, markdownReport);
  }
}

console.log('\n[host-runtime] 运行态健康检查通过。');
