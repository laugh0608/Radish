import fs from 'node:fs/promises';
import process from 'node:process';

import { collectChangedFiles } from './changed-files.mjs';
import {
  collectIdentityImpactReasonGroups,
  normalizePath,
} from './identity-impact-rules.mjs';
import { writeSummaryActionReport } from './m14-reporting.mjs';
import { runCommand } from './process-runner.mjs';
import { parseSummaryActionReport } from './summary-action-report.mjs';

const args = process.argv.slice(2);
const repoRoot = process.cwd();

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
  console.log('用法: node Scripts/collect-change-regression-record.mjs [options]');
  console.log('');
  console.log('默认输入 / 输出：');
  console.log('  --mode                 worktree');
  console.log('  --ci-report            .tmp/validate-ci-report.md');
  console.log('  --baseline-quick-report <disabled>');
  console.log('  --baseline-report      <disabled>');
  console.log('  --baseline-host-report <disabled>');
  console.log('  --host-record          <disabled>');
  console.log('  --output               .tmp/change-regression-record.md');
  console.log('');
  console.log('可选参数：');
  console.log('  --title <text>         记录标题，默认“当前批次”');
  console.log('  --scope <text>         变更范围，默认“当前 worktree 改动批次”');
  console.log('  --author <text>        记录人，默认自动读取 git user.name');
  console.log('  --date <YYYY-MM-DD>    记录日期，默认 Asia/Shanghai 当日');
  console.log('  --mode <mode>          changed files 模式：worktree / staged / tracked');
  console.log('  --ci-report <path>     validate:ci 报告路径');
  console.log('  --baseline-quick-report <path>');
  console.log('  --baseline-report <path>');
  console.log('  --baseline-host-report <path>');
  console.log('  --host-record <path>   M14 宿主维护记录路径');
  console.log('  --output <path>        输出 Markdown 路径');
  console.log('  --print                同时打印到终端');
}

function uniqueValues(values) {
  return [...new Set(values.filter((value) => typeof value === 'string' && value.trim().length > 0))];
}

function formatDateInShanghai() {
  const formatter = new Intl.DateTimeFormat('en-US', {
    timeZone: 'Asia/Shanghai',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  });
  const parts = formatter.formatToParts(new Date());
  const year = parts.find((part) => part.type === 'year')?.value ?? '0000';
  const month = parts.find((part) => part.type === 'month')?.value ?? '00';
  const day = parts.find((part) => part.type === 'day')?.value ?? '00';
  return `${year}-${month}-${day}`;
}

function resolveAuthorName(explicitAuthor) {
  if (explicitAuthor.trim().length > 0) {
    return explicitAuthor;
  }

  const envAuthor = process.env.GIT_AUTHOR_NAME ||
    process.env.GIT_COMMITTER_NAME ||
    process.env.USERNAME ||
    process.env.USER;

  if (typeof envAuthor === 'string' && envAuthor.trim().length > 0) {
    return envAuthor.trim();
  }

  const result = runCommand('git', ['config', 'user.name'], {
    cwd: repoRoot,
    encoding: 'utf8',
    stdio: 'pipe',
  });

  if (result.status === 0) {
    const value = result.stdout?.toString().trim();
    if (value) {
      return value;
    }
  }

  return 'unknown';
}

async function readOptionalReport(reportPath) {
  if (!reportPath || reportPath.trim().length === 0) {
    return {
      exists: false,
      parsed: null,
      markdown: '',
    };
  }

  try {
    const markdown = await fs.readFile(reportPath, 'utf8');
    return {
      exists: true,
      parsed: parseSummaryActionReport(markdown, reportPath),
      markdown,
    };
  } catch (error) {
    if (error && typeof error === 'object' && 'code' in error && error.code === 'ENOENT') {
      return {
        exists: false,
        parsed: null,
        markdown: '',
      };
    }

    throw error;
  }
}

async function readOptionalHostRecord(reportPath) {
  if (!reportPath || reportPath.trim().length === 0) {
    return {
      exists: false,
      parsed: null,
      markdown: '',
    };
  }

  try {
    const markdown = await fs.readFile(reportPath, 'utf8');
    return {
      exists: true,
      parsed: parseSummaryActionReport(markdown, reportPath, {
        summaryHeading: '#### Overview',
        actionHeading: '#### Combined Actions',
      }),
      markdown,
    };
  } catch (error) {
    if (error && typeof error === 'object' && 'code' in error && error.code === 'ENOENT') {
      return {
        exists: false,
        parsed: null,
        markdown: '',
      };
    }

    throw error;
  }
}

function summarizePathScope(files) {
  const buckets = [];

  for (const file of files) {
    const normalized = normalizePath(file);
    const parts = normalized.split('/');

    if (normalized.startsWith('Frontend/') && parts.length >= 2) {
      buckets.push(`${parts[0]}/${parts[1]}`);
      continue;
    }

    if (normalized.startsWith('Docs/') && parts.length >= 2) {
      buckets.push(`${parts[0]}/${parts[1]}`);
      continue;
    }

    buckets.push(parts[0] || normalized);
  }

  return uniqueValues(buckets).slice(0, 6);
}

function buildChangeSummaryLines(files, changedFilesEvaluated) {
  if (!changedFilesEvaluated) {
    return ['- 当前环境未成功评估 changed files；请在允许子进程的环境重跑，或改用显式报告路径补齐当前批次事实。'];
  }

  if (files.length === 0) {
    return ['- 当前批次未识别到变更文件；如需针对已提交批次生成记录，请改用 `--mode=tracked` 或在目标 worktree 中执行。'];
  }

  const scopeBuckets = summarizePathScope(files);
  const lines = [`- 当前批次共识别 ${files.length} 个变更文件。`];

  if (scopeBuckets.length > 0) {
    lines.push(`- 主要路径范围：${scopeBuckets.map((item) => `\`${item}\``).join('、')}。`);
  }

  return lines;
}

function buildTopicLines(reportMap, reasonGroups) {
  const topics = [];

  if (reportMap.ci?.parsed) {
    topics.push('Repo Quality / validate:ci');
  }

  if (reportMap.baselineQuick?.parsed || reportMap.baselineFull?.parsed || reportMap.baselineHost?.parsed) {
    topics.push('默认验证基线');
  }

  if (reasonGroups.length > 0) {
    topics.push('身份语义影响面');
  }

  if (reportMap.hostRecord?.parsed) {
    topics.push('M14 宿主 / 部署后最小复核');
  }

  return topics.length > 0
    ? topics.map((topic) => `- ${topic}`)
    : ['- 当前未读取到自动化报告；如需补齐本节，请先生成对应报告。'];
}

function getIdentitySectionLines(reasonGroups, files, changedFilesEvaluated) {
  if (!changedFilesEvaluated) {
    return [
      '- 命中情况：未评估',
      '- 命中原因：当前环境未成功评估 changed files',
      '- 变更文件数：not-evaluated',
    ];
  }

  if (reasonGroups.length === 0) {
    return [
      '- 命中情况：未命中',
      '- 命中原因：无',
      `- 变更文件数：${files.length}`,
    ];
  }

  const labels = reasonGroups.map((group) => group.label);
  return [
    '- 命中情况：命中',
    `- 命中原因：${labels.join(' / ')}`,
    `- 命中原因类别：${reasonGroups.length}`,
  ];
}

function buildAutomationStatusLines(reportMap) {
  const lines = [];
  const statusMap = new Map([
    ['npm run validate:baseline:quick', '未执行'],
    ['npm run validate:baseline', '未执行'],
    ['npm run validate:baseline:host', '未执行'],
    ['npm run validate:ci', '未执行'],
  ]);
  const detailMap = new Map();

  if (reportMap.baselineQuick?.parsed) {
    statusMap.set('npm run validate:baseline:quick', reportMap.baselineQuick.parsed.overall === 'passed' ? '通过' : '阻塞');
    detailMap.set('npm run validate:baseline:quick', reportMap.baselineQuick.parsed.sourcePath);
  }

  if (reportMap.baselineFull?.parsed) {
    statusMap.set('npm run validate:baseline', reportMap.baselineFull.parsed.overall === 'passed' ? '通过' : '阻塞');
    detailMap.set('npm run validate:baseline', reportMap.baselineFull.parsed.sourcePath);
  }

  if (reportMap.baselineHost?.parsed) {
    statusMap.set('npm run validate:baseline:host', reportMap.baselineHost.parsed.overall === 'passed' ? '通过' : '阻塞');
    detailMap.set('npm run validate:baseline:host', reportMap.baselineHost.parsed.sourcePath);
  }

  if (reportMap.ci?.parsed) {
    statusMap.set('npm run validate:ci', reportMap.ci.parsed.overall === 'passed' ? '通过' : '阻塞');
    detailMap.set('npm run validate:ci', reportMap.ci.parsed.sourcePath);
  }

  for (const [command, status] of statusMap.entries()) {
    const sourcePath = detailMap.get(command);
    lines.push(`- \`${command}\`：${status}${sourcePath ? `（来源：${sourcePath}）` : ''}`);
  }

  return lines;
}

function inferFailureClassification(reportEntries) {
  const reports = reportEntries
    .map((entry) => entry?.parsed)
    .filter(Boolean);

  if (reports.length === 0 || reports.every((report) => report.overall !== 'failed')) {
    return {
      label: '无',
      description: '无',
    };
  }

  const allMarkdown = reportEntries
    .map((entry) => entry?.markdown ?? '')
    .join('\n');

  if (allMarkdown.includes('受限环境') || allMarkdown.includes('EPERM') || allMarkdown.includes('EINVAL')) {
    const source = reports.find((report) => report.overall === 'failed');
    return {
      label: '受限环境边界',
      description: source
        ? `\`${source.title || source.route || 'report'}\` 失败，报告动作提示需先按受限环境边界处理。`
        : '报告中出现受限环境边界提示。',
    };
  }

  if (reports.some((report) => report.triageCode.some((code) => code.includes('contract')))) {
    const source = reports.find((report) => report.triageCode.some((code) => code.includes('contract')));
    return {
      label: 'contract 漂移',
      description: source
        ? `\`${source.title || source.route || 'report'}\` 报告命中了 contract 漂移相关分诊码。`
        : '报告命中了 contract 漂移相关分诊码。',
    };
  }

  if (reports.some((report) => report.triageScope.includes('identity-regression'))) {
    const source = reports.find((report) => report.triageScope.includes('identity-regression'));
    return {
      label: '身份语义专题失败',
      description: source
        ? `\`${source.title || source.route || 'report'}\` 报告命中了身份语义专题分诊范围。`
        : '报告命中了身份语义专题分诊范围。',
    };
  }

  const source = reports.find((report) => report.overall === 'failed');
  return {
    label: '默认执行面失败',
    description: source
      ? `\`${source.title || source.route || 'report'}\` 当前处于失败状态；请按其 Summary / Actions 继续分诊。`
      : '自动化报告存在失败项，请按 Summary / Actions 继续分诊。',
  };
}

function buildConclusion(reportEntries, failure) {
  const reports = reportEntries
    .map((entry) => entry?.parsed)
    .filter(Boolean);

  if (reports.length === 0) {
    return '- 当前尚未读取到自动化报告，暂不能给出“可合并”结论。';
  }

  if (reports.every((report) => report.overall === 'passed')) {
    return '- 当前已读取到的自动化报告均通过；如专题回归与人工验收无额外阻塞，可作为本批次合并判断依据。';
  }

  if (failure.label === '受限环境边界') {
    return '- 当前自动化结论受限于运行环境边界；需要在允许子进程的环境复核后，再给出最终合并判断。';
  }

  return '- 当前自动化报告中仍存在阻塞项，本批次暂不应直接下“可合并”结论。';
}

function buildRiskLines(reportMap) {
  const lines = ['- 无'];

  if (reportMap.hostRecord?.parsed) {
    lines[0] = '- 若本轮同时涉及真实部署或宿主配置，请继续核对 `M14` 维护记录中的后续动作。';
  }

  return lines;
}

function renderSection(title, lines) {
  return [
    `### ${title}`,
    '',
    ...lines,
    '',
  ];
}

function buildMarkdown({
  title,
  recordDate,
  author,
  scope,
  changedFiles,
  changedFilesEvaluated,
  reasonGroups,
  reportMap,
}) {
  const failure = inferFailureClassification(Object.values(reportMap));
  const hostRecord = reportMap.hostRecord?.parsed;

  const sections = [
    `## ${title} 变更回归记录`,
    '',
    `- 记录日期：${recordDate}`,
    `- 记录人：${author}`,
    `- 变更范围：${scope}`,
    '',
    ...renderSection('变更摘要', buildChangeSummaryLines(changedFiles, changedFilesEvaluated)),
    ...renderSection('影响专题', buildTopicLines(reportMap, reasonGroups)),
    ...renderSection('身份语义影响面（按需）', getIdentitySectionLines(reasonGroups, changedFiles, changedFilesEvaluated)),
    ...renderSection('自动化执行', buildAutomationStatusLines(reportMap)),
    ...renderSection('专题回归', [
      '- 无（当前脚本只汇总自动化报告；如本轮还执行了 `HttpTest`、专题脚本或专题手册，请手动补充）。',
    ]),
    ...renderSection('人工验收', [
      '- 执行情况：未执行',
      '- 摘要：当前脚本不自动推断人工验收，请按本轮真实执行动作补充。',
    ]),
    ...renderSection('部署复核（按需）', hostRecord
      ? [
          '- 执行情况：已执行',
          `- 记录入口：${hostRecord.sourcePath}`,
          `- 摘要：Overall=${hostRecord.overall || 'unknown'}；NextStage=${hostRecord.nextStage || 'unknown'}`,
        ]
      : [
          '- 执行情况：未执行',
          '- 记录入口：无',
          '- 摘要：当前未读取到 `M14` 宿主维护记录。',
        ]),
    ...renderSection('故障归类 / 环境边界（按需）', [
      `- 归类：${failure.label}`,
      `- 说明：${failure.description}`,
    ]),
    ...renderSection('结论', [buildConclusion(Object.values(reportMap), failure)]),
    ...renderSection('风险 / 后置项', buildRiskLines(reportMap)),
  ];

  return sections.join('\n').trimEnd();
}

if (hasFlag('--help') || hasFlag('-h')) {
  printUsage();
  process.exit(0);
}

const title = getArgValue('--title', '当前批次');
const scope = getArgValue('--scope', '当前 worktree 改动批次');
const author = resolveAuthorName(getArgValue('--author', ''));
const recordDate = getArgValue('--date', formatDateInShanghai());
const mode = getArgValue('--mode', 'worktree');
const outputFile = getArgValue('--output', '.tmp/change-regression-record.md');
const shouldPrint = hasFlag('--print');

const reportFiles = {
  ci: getArgValue('--ci-report', '.tmp/validate-ci-report.md'),
  baselineQuick: getArgValue('--baseline-quick-report', ''),
  baselineFull: getArgValue('--baseline-report', ''),
  baselineHost: getArgValue('--baseline-host-report', ''),
  hostRecord: getArgValue('--host-record', ''),
};

let changedFiles = [];
let changedFilesEvaluated = false;
try {
  changedFiles = collectChangedFiles({
    repoRoot,
    mode,
  }).map(normalizePath);
  changedFilesEvaluated = true;
} catch (error) {
  console.warn(error instanceof Error ? error.message : String(error));
}

const reasonGroups = collectIdentityImpactReasonGroups(changedFiles);
const reportMap = {
  ci: await readOptionalReport(reportFiles.ci),
  baselineQuick: await readOptionalReport(reportFiles.baselineQuick),
  baselineFull: await readOptionalReport(reportFiles.baselineFull),
  baselineHost: await readOptionalReport(reportFiles.baselineHost),
  hostRecord: await readOptionalHostRecord(reportFiles.hostRecord),
};

const markdown = buildMarkdown({
  title,
  recordDate,
  author,
  scope,
  changedFiles,
  changedFilesEvaluated,
  reasonGroups,
  reportMap,
});

await writeSummaryActionReport('change-record', outputFile, markdown);

if (shouldPrint) {
  console.log('\n[change-record] ----- BEGIN RECORD -----');
  console.log(markdown);
  console.log('[change-record] ----- END RECORD -----');
}
