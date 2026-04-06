import fs from 'node:fs/promises';
import path from 'node:path';

function uniqueValues(values) {
  return [...new Set(values.filter((value) => typeof value === 'string' && value.trim().length > 0))];
}

function formatFieldValue(values, emptyValue = 'none') {
  const unique = uniqueValues(Array.isArray(values) ? values : [values]);
  return unique.length > 0 ? unique.join(', ') : emptyValue;
}

export function buildTriageSummaryLines({ route, findings, nextStage }) {
  return [
    `- Route: ${route}`,
    `- TriageScope: ${formatFieldValue(findings.map((finding) => finding.scope))}`,
    `- TriageCode: ${formatFieldValue(findings.map((finding) => finding.code))}`,
    `- NextStage: ${nextStage}`,
  ];
}

export function buildSummaryActionReport({ title, summaryLines, actionLines }) {
  return [
    `### ${title}`,
    '',
    '#### Summary',
    ...summaryLines,
    '',
    '#### Actions',
    ...actionLines,
  ].join('\n');
}

export function printSummaryActionReport(prefix, markdownReport) {
  console.error(`\n[${prefix}] ----- BEGIN REPORT -----`);
  console.error(markdownReport);
  console.error(`[${prefix}] ----- END REPORT -----`);
}

export async function writeSummaryActionReport(prefix, reportFile, markdownReport) {
  const resolvedReportFile = path.resolve(reportFile);
  await fs.mkdir(path.dirname(resolvedReportFile), { recursive: true });
  await fs.writeFile(resolvedReportFile, `${markdownReport}\n`, 'utf8');
  console.error(`[${prefix}] 报告已写入: ${resolvedReportFile}`);
  return resolvedReportFile;
}
