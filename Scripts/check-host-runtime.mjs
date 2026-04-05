import fs from 'node:fs/promises';
import http from 'node:http';
import https from 'node:https';
import path from 'node:path';
import process from 'node:process';

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
  console.log('  --details              当默认检查失败时，追加抓取 Gateway /healthz 明细摘要');
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

function deriveGatewayDetailsUrl(gatewayHealthUrl) {
  const url = new URL(gatewayHealthUrl);
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

function getGatewayDetailsSummary(result) {
  if (!result.ok) {
    const category = inferFailureCategory(result);
    return {
      ok: false,
      summary: null,
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
      error: {
        statusCode: result.statusCode || 0,
        category: {
          code: 'invalid-json',
          label: 'healthz 响应不是可解析 JSON',
          nextStep: '先确认 Gateway /healthz 是否已切到结构化 JSON 输出，或是否被其他反代层改写了响应。',
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
    summary: {
      overallStatus: payload.status ?? 'unknown',
      generatedAtUtc: payload.generatedAtUtc ?? 'n/a',
      totalDurationMs: Number.isFinite(payload.totalDurationMs) ? payload.totalDurationMs : null,
      entries,
    },
  };
}

function printGatewayDetailsSummary(result) {
  const details = getGatewayDetailsSummary(result);
  if (!details.ok) {
    console.error(`[host-runtime] /healthz 拉取失败 (${details.error.statusCode || 'no-response'})`);
    console.error(`[host-runtime]   分类: ${details.error.category.label} (${details.error.category.code})`);
    if (details.error.response) {
      console.error(`[host-runtime]   响应: ${details.error.response}`);
    }
    return details;
  }

  console.error('[host-runtime] Gateway /healthz 明细：');
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

function buildGatewayDetailsReportLines(details) {
  if (!details) {
    return [];
  }

  if (!details.ok) {
    const response = details.error.response ? ` | 响应: ${details.error.response}` : '';
    return [
      `- Gateway /healthz: 拉取失败 (${details.error.statusCode || 'no-response'}) | 分类: ${details.error.category.label} (${details.error.category.code})${response}`,
    ];
  }

  const lines = [
    `- Gateway /healthz: ${details.summary.overallStatus} | generatedAtUtc: ${details.summary.generatedAtUtc} | total: ${formatDurationMs(details.summary.totalDurationMs)}`,
  ];

  for (const entry of details.summary.entries) {
    const tags = entry.tags.length > 0 ? entry.tags.join(', ') : 'none';
    const extras = [entry.description, entry.exception]
      .filter((item) => item);
    const suffix = extras.length > 0 ? ` | ${extras.join(' | ')}` : '';
    lines.push(`- Gateway /healthz entry: ${entry.name} | ${entry.status} | tags: ${tags} | duration: ${formatDurationMs(entry.durationMs)}${suffix}`);
  }

  return lines;
}

function buildActionLines(results, gatewayDetails) {
  const lines = [];

  for (const result of results) {
    if (result.ok) {
      continue;
    }

    const category = inferFailureCategory(result);
    lines.push(`- ${result.name}: ${category.nextStep}`);
  }

  if (gatewayDetails?.ok) {
    const degradedEntries = gatewayDetails.summary.entries.filter(
      (entry) => entry.status !== 'Healthy'
    );

    for (const entry of degradedEntries) {
      const reason = [entry.description, entry.exception]
        .filter((item) => item)
        .join(' | ');
      const suffix = reason ? ` 当前摘要: ${reason}` : '';
      lines.push(`- Gateway /healthz ${entry.name}: 优先确认该扩展下游是否应在本轮启动；若不属于最小宿主链，可按 M14 口径单独记录为降级观测项。${suffix}`);
    }
  }

  if (gatewayDetails && !gatewayDetails.ok) {
    lines.push(`- Gateway /healthz: ${gatewayDetails.error.category.nextStep}`);
  }

  if (lines.length === 0) {
    lines.push('- 当前运行态健康检查通过，可继续进入更上层联调或保留为本轮复核记录。');
  }

  return [...new Set(lines)];
}

function buildMarkdownReport({ executedAtUtc, results, gatewayDetails, strictTls, timeoutMs }) {
  const overallStatus = results.every((result) => result.ok) ? 'passed' : 'failed';
  const summaryLines = [
    ...results.map((result) => buildResultReportLine(result)),
    ...buildGatewayDetailsReportLines(gatewayDetails),
  ];
  const actionLines = buildActionLines(results, gatewayDetails);

  const lines = [
    '### Host Runtime Check',
    '',
    '#### Summary',
    `- Time: ${executedAtUtc}`,
    `- Overall: ${overallStatus}`,
    `- TimeoutMs: ${timeoutMs}`,
    `- StrictTls: ${strictTls ? 'true' : 'false'}`,
    ...summaryLines,
    '',
    '#### Actions',
    ...actionLines,
  ];

  return lines.join('\n');
}

function printMarkdownReport(markdownReport) {
  console.error('\n[host-runtime] ----- BEGIN REPORT -----');
  console.error(markdownReport);
  console.error('[host-runtime] ----- END REPORT -----');
}

async function writeMarkdownReport(reportFile, markdownReport) {
  const resolvedReportFile = path.resolve(reportFile);
  await fs.mkdir(path.dirname(resolvedReportFile), { recursive: true });
  await fs.writeFile(resolvedReportFile, `${markdownReport}\n`, 'utf8');
  console.error(`[host-runtime] 报告已写入: ${resolvedReportFile}`);
  return resolvedReportFile;
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
const gatewayDetailsTarget = {
  name: 'GatewayDetails',
  url: deriveGatewayDetailsUrl(targets[0].url),
};
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
let gatewayDetailsSummary = null;

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
    console.error(`\n[host-runtime] 追加抓取 Gateway 明细：`);
    console.error(`> GET ${gatewayDetailsTarget.url}`);
    const detailsResult = await requestHealth(gatewayDetailsTarget, detailsTimeoutMs, strictTls);
    gatewayDetailsSummary = printGatewayDetailsSummary(detailsResult);
  }

  if (showReport) {
    const markdownReport = buildMarkdownReport({
      executedAtUtc,
      results,
      gatewayDetails: gatewayDetailsSummary,
      strictTls,
      timeoutMs,
    });
    printMarkdownReport(markdownReport);
    if (reportFile) {
      await writeMarkdownReport(reportFile, markdownReport);
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
    gatewayDetails: gatewayDetailsSummary,
    strictTls,
    timeoutMs,
  });
  printMarkdownReport(markdownReport);
  if (reportFile) {
    await writeMarkdownReport(reportFile, markdownReport);
  }
}

console.log('\n[host-runtime] 运行态健康检查通过。');
