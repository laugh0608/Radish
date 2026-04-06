import fs from 'node:fs/promises';
import path from 'node:path';
import process from 'node:process';

import { writeSummaryActionReport } from './m14-reporting.mjs';

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
  console.log('用法: node Scripts/collect-m14-host-record.mjs [options]');
  console.log('');
  console.log('默认输入 / 输出：');
  console.log('  --baseline-report .tmp/baseline-host-report.md');
  console.log('  --runtime-report  .tmp/host-runtime-report.md');
  console.log('  --output          .tmp/m14-host-maintenance-record.md');
  console.log('');
  console.log('可选参数：');
  console.log('  --baseline-report <path>  指定启动前报告路径');
  console.log('  --runtime-report <path>   指定运行态报告路径');
  console.log('  --output <path>           指定汇总记录输出路径');
  console.log('  --print                   同时把汇总记录打印到终端');
}

function normalizeLineEndings(text) {
  return text.replace(/\r\n/g, '\n');
}

function extractSection(lines, heading) {
  const startIndex = lines.indexOf(heading);
  if (startIndex < 0) {
    return [];
  }

  const sectionLines = [];
  for (let index = startIndex + 1; index < lines.length; index += 1) {
    const line = lines[index];
    if (line.startsWith('#### ')) {
      break;
    }

    if (sectionLines.length === 0 && line.trim() === '') {
      continue;
    }

    sectionLines.push(line);
  }

  while (sectionLines.length > 0 && sectionLines.at(-1)?.trim() === '') {
    sectionLines.pop();
  }

  return sectionLines;
}

function getFieldValue(lines, fieldName) {
  const prefix = `- ${fieldName}:`;
  const line = lines.find((item) => item.startsWith(prefix));
  return line ? line.slice(prefix.length).trim() : '';
}

function splitFieldValues(value) {
  if (!value || value === 'none') {
    return [];
  }

  return value
    .split(',')
    .map((item) => item.trim())
    .filter((item) => item.length > 0);
}

function uniqueValues(values) {
  return [...new Set(values.filter((value) => typeof value === 'string' && value.trim().length > 0))];
}

function formatList(values, emptyValue = 'none') {
  const unique = uniqueValues(values);
  return unique.length > 0 ? unique.join(', ') : emptyValue;
}

function inferOverall(reports) {
  const overallValues = reports
    .map((report) => report.overall)
    .filter((value) => value.length > 0);

  if (overallValues.includes('failed')) {
    return 'failed';
  }

  if (overallValues.length > 0 && overallValues.every((value) => value === 'passed')) {
    return 'passed';
  }

  return 'partial';
}

function inferNextStage(reports) {
  const baselineReport = reports.find((report) => report.route === 'preflight');
  const runtimeReport = reports.find((report) => report.route === 'runtime');

  if (baselineReport?.overall === 'failed') {
    return baselineReport.nextStage || 'fix-preflight-and-rerun';
  }

  if (runtimeReport?.overall === 'failed') {
    return runtimeReport.nextStage || 'inspect-runtime-and-host-logs';
  }

  if (runtimeReport) {
    return runtimeReport.nextStage || 'record-and-close';
  }

  if (baselineReport) {
    return baselineReport.nextStage || 'run-runtime-check';
  }

  return 'record-and-close';
}

function buildCombinedActions(reports) {
  const baselineReport = reports.find((report) => report.route === 'preflight');
  const runtimeReport = reports.find((report) => report.route === 'runtime');

  if (baselineReport?.overall === 'failed') {
    return baselineReport.actions;
  }

  if (runtimeReport?.overall === 'failed') {
    return runtimeReport.actions;
  }

  if (baselineReport && runtimeReport) {
    return ['- 当前启动前与启动后主路径均已闭合，可将本文件直接作为本轮维护记录留档。'];
  }

  if (baselineReport) {
    return baselineReport.actions;
  }

  if (runtimeReport) {
    return runtimeReport.actions;
  }

  return ['- 当前没有可汇总的 M14 报告。'];
}

function buildReportSection(title, lines) {
  return [
    `#### ${title}`,
    ...(lines.length > 0 ? lines : ['- none']),
  ];
}

function parseReport(markdown, sourcePath) {
  const lines = normalizeLineEndings(markdown).split('\n');
  const summaryLines = extractSection(lines, '#### Summary');
  const actionLines = extractSection(lines, '#### Actions');

  if (summaryLines.length === 0 || actionLines.length === 0) {
    throw new Error(`无法从报告中识别 Summary / Actions：${sourcePath}`);
  }

  return {
    sourcePath: path.resolve(sourcePath),
    summary: summaryLines,
    actions: actionLines,
    route: getFieldValue(summaryLines, 'Route'),
    overall: getFieldValue(summaryLines, 'Overall'),
    nextStage: getFieldValue(summaryLines, 'NextStage'),
    triageScope: splitFieldValues(getFieldValue(summaryLines, 'TriageScope')),
    triageCode: splitFieldValues(getFieldValue(summaryLines, 'TriageCode')),
  };
}

function buildMergedMarkdownReport({ executedAtUtc, reports }) {
  const routeValues = reports.map((report) => report.route);
  const scopeValues = reports.flatMap((report) => report.triageScope);
  const codeValues = reports.flatMap((report) => report.triageCode);
  const overall = inferOverall(reports);
  const nextStage = inferNextStage(reports);
  const combinedActions = buildCombinedActions(reports);

  const lines = [
    '### M14 Host Maintenance Record',
    '',
    '#### Overview',
    `- Time: ${executedAtUtc}`,
    `- Overall: ${overall}`,
    `- Route: ${formatList(routeValues)}`,
    `- TriageScope: ${formatList(scopeValues)}`,
    `- TriageCode: ${formatList(codeValues)}`,
    `- NextStage: ${nextStage}`,
    ...reports.map((report) => `- Source (${report.route || 'unknown'}): ${report.sourcePath}`),
    '',
    '#### Combined Actions',
    ...combinedActions,
    '',
    ...buildReportSection('Preflight Summary', reports.find((report) => report.route === 'preflight')?.summary || []),
    '',
    ...buildReportSection('Preflight Actions', reports.find((report) => report.route === 'preflight')?.actions || []),
    '',
    ...buildReportSection('Runtime Summary', reports.find((report) => report.route === 'runtime')?.summary || []),
    '',
    ...buildReportSection('Runtime Actions', reports.find((report) => report.route === 'runtime')?.actions || []),
  ];

  return lines.join('\n');
}

if (hasFlag('--help') || hasFlag('-h')) {
  printUsage();
  process.exit(0);
}

const baselineReportFile = getArgValue('--baseline-report', '.tmp/baseline-host-report.md');
const runtimeReportFile = getArgValue('--runtime-report', '.tmp/host-runtime-report.md');
const outputFile = getArgValue('--output', '.tmp/m14-host-maintenance-record.md');
const shouldPrint = hasFlag('--print');

const reports = [];

for (const reportConfig of [
  { route: 'preflight', file: baselineReportFile },
  { route: 'runtime', file: runtimeReportFile },
]) {
  try {
    const markdown = await fs.readFile(reportConfig.file, 'utf8');
    const parsed = parseReport(markdown, reportConfig.file);
    reports.push({
      ...parsed,
      route: parsed.route || reportConfig.route,
    });
    console.log(`[m14-record] 已读取 ${reportConfig.route} 报告：${path.resolve(reportConfig.file)}`);
  } catch (error) {
    if (error && typeof error === 'object' && 'code' in error && error.code === 'ENOENT') {
      console.warn(`[m14-record] 未找到 ${reportConfig.route} 报告：${path.resolve(reportConfig.file)}`);
      continue;
    }

    throw error;
  }
}

if (reports.length === 0) {
  console.error('[m14-record] 未找到任何可汇总的 M14 报告。');
  console.error('[m14-record] 请先执行 `npm run validate:baseline:host -- --report-file ...` 或 `npm run check:host-runtime -- --report-file ...`。');
  process.exit(1);
}

const markdownReport = buildMergedMarkdownReport({
  executedAtUtc: new Date().toISOString(),
  reports,
});

await writeSummaryActionReport('m14-record', outputFile, markdownReport);

if (shouldPrint) {
  console.log('\n[m14-record] ----- BEGIN RECORD -----');
  console.log(markdownReport);
  console.log('[m14-record] ----- END RECORD -----');
}
