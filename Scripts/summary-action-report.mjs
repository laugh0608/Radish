import path from 'node:path';

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

export function getFieldValue(lines, fieldName) {
  const prefix = `- ${fieldName}:`;
  const line = lines.find((item) => item.startsWith(prefix));
  return line ? line.slice(prefix.length).trim() : '';
}

export function splitFieldValues(value) {
  if (!value || value === 'none') {
    return [];
  }

  return value
    .split(',')
    .map((item) => item.trim())
    .filter((item) => item.length > 0);
}

export function parseSummaryActionReport(
  markdown,
  sourcePath,
  {
    summaryHeading = '#### Summary',
    actionHeading = '#### Actions',
  } = {}
) {
  const lines = normalizeLineEndings(markdown).split('\n');
  const titleLine = lines.find((line) => line.startsWith('### ')) ?? '';
  const summaryLines = extractSection(lines, summaryHeading);
  const actionLines = extractSection(lines, actionHeading);

  if (summaryLines.length === 0 || actionLines.length === 0) {
    throw new Error(`无法从报告中识别 ${summaryHeading} / ${actionHeading}：${sourcePath}`);
  }

  return {
    sourcePath: path.resolve(sourcePath),
    title: titleLine.startsWith('### ') ? titleLine.slice('### '.length).trim() : '',
    summary: summaryLines,
    actions: actionLines,
    route: getFieldValue(summaryLines, 'Route'),
    overall: getFieldValue(summaryLines, 'Overall'),
    nextStage: getFieldValue(summaryLines, 'NextStage'),
    triageScope: splitFieldValues(getFieldValue(summaryLines, 'TriageScope')),
    triageCode: splitFieldValues(getFieldValue(summaryLines, 'TriageCode')),
  };
}
