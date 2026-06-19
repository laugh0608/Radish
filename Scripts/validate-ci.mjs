import process from 'node:process';

import {
  buildSummaryActionReport,
  buildTriageSummaryLines,
  printSummaryActionReport,
  writeSummaryActionReport,
} from './m14-reporting.mjs';
import {
  collectBackendImpactDetails,
  collectBackendImpactMatches,
  collectBackendImpactReasonGroups,
} from './backend-impact-rules.mjs';
import {
  collectIdentityImpactDetails,
  collectIdentityImpactMatches,
  collectIdentityImpactReasonGroups,
} from './identity-impact-rules.mjs';
import { formatCommand, runCommand } from './process-runner.mjs';
import {
  BACKEND_GUARD_CHECK_NAME,
  BACKEND_GUARD_VALIDATE_ARGS,
  IDENTITY_GUARD_CHECK_NAME,
  IDENTITY_GUARD_VALIDATE_ARGS,
  REPO_QUALITY_LOCAL_STEPS,
} from './repo-quality-contract.mjs';

const repoRoot = process.cwd();
const rawArgs = process.argv.slice(2);
const args = new Set(rawArgs);

function hasFlag(flag) {
  return args.has(flag);
}

function getArgValue(flagName, defaultValue) {
  const directPrefix = `${flagName}=`;
  const direct = rawArgs.find((arg) => arg.startsWith(directPrefix));
  if (direct) {
    return direct.slice(directPrefix.length);
  }

  const index = rawArgs.indexOf(flagName);
  if (index >= 0 && index + 1 < rawArgs.length) {
    return rawArgs[index + 1];
  }

  return defaultValue;
}

function printUsage() {
  console.log('用法: node Scripts/validate-ci.mjs [--report] [--report-file <path>]');
  console.log('');
  console.log('--report            输出可直接回写到记录/PR 的固定 Markdown 报告');
  console.log('--report-file       将 Markdown 报告直接写入指定文件（会自动启用 --report）');
}

function runNode(commandArgs, options = {}) {
  const result = runCommand('node', commandArgs, {
    cwd: repoRoot,
    stdio: options.captureStdout ? ['ignore', 'pipe', 'inherit'] : 'inherit',
  });

  if (result.error) {
    return {
      ok: false,
      status: 1,
      stdout: Buffer.alloc(0),
      error: result.error,
    };
  }

  if (result.status !== 0) {
    return {
      ok: false,
      status: result.status ?? 1,
      stdout: result.stdout ?? Buffer.alloc(0),
      error: null,
    };
  }

  return {
    ok: true,
    status: 0,
    stdout: result.stdout ?? Buffer.alloc(0),
    error: null,
  };
}

function runNpm(title, args) {
  console.log(`\n[validate:ci] ${title}`);
  console.log(`> ${formatCommand('npm', args)}`);

  const result = runCommand('npm', args, {
    cwd: repoRoot,
    stdio: 'inherit',
  });

  if (result.error) {
    return {
      ok: false,
      status: 1,
      title,
      error: result.error,
    };
  }

  if (result.status !== 0) {
    return {
      ok: false,
      status: result.status ?? 1,
      title,
      error: null,
    };
  }

  return {
    ok: true,
    status: 0,
    title,
    error: null,
  };
}

function splitZeroTerminated(buffer) {
  return buffer
    .toString('utf8')
    .split('\0')
    .map((file) => file.trim())
    .filter(Boolean);
}

function buildStepSummaryLine(outcome) {
  return `- ${outcome.title}: ${outcome.ok ? '正常' : `失败 (${outcome.status})`}`;
}

function formatCount(value) {
  return typeof value === 'number' ? String(value) : 'not-evaluated';
}

function getConditionalGuardMode(matchedFiles) {
  if (!Array.isArray(matchedFiles)) {
    return 'not-evaluated';
  }

  return matchedFiles.length === 0 ? 'skipped' : 'validated';
}

function getValidateCiFindings(failedPhase, backendMatchedFiles, identityMatchedFiles) {
  if (!failedPhase) {
    const findings = [];
    findings.push(
      backendMatchedFiles.length === 0
        ? { scope: 'repo-quality-local', code: 'backend-guard-skipped' }
        : { scope: 'backend-regression', code: 'backend-validated' }
    );
    findings.push(
      identityMatchedFiles.length === 0
        ? { scope: 'repo-quality-local', code: 'identity-guard-skipped' }
        : { scope: 'identity-regression', code: 'identity-validated' }
    );

    return findings;
  }

  if (failedPhase === 'repo-hygiene') {
    return [
      { scope: 'repo-quality-local', code: 'repo-hygiene-phase-failed' },
    ];
  }

  if (failedPhase === 'frontend-lint') {
    return [
      { scope: 'repo-quality-local', code: 'frontend-lint-phase-failed' },
    ];
  }

  if (failedPhase === 'baseline-quick') {
    return [
      { scope: 'repo-quality-local', code: 'baseline-quick-phase-failed' },
    ];
  }

  if (failedPhase === 'collect-changed-files') {
    return [
      { scope: 'repo-quality-local', code: 'collect-changed-files-failed' },
    ];
  }

  if (failedPhase === 'backend-guard') {
    return [
      { scope: 'backend-regression', code: 'backend-guard-failed' },
    ];
  }

  return [
    { scope: 'identity-regression', code: 'identity-guard-failed' },
  ];
}

function getValidateCiActionLines(failedPhase, backendMatchedFiles, identityMatchedFiles) {
  if (failedPhase === 'repo-hygiene') {
    return [
      '- 先查看 `Repo Hygiene changed-only` 的原始输出，判断这是文本卫生失败，还是受限环境边界导致的子进程失败。',
      '- 若输出包含“当前受限环境禁止从 Node 脚本再拉起外部进程”，按“受限环境边界”归类，不要误改业务代码。',
    ];
  }

  if (failedPhase === 'frontend-lint') {
    return [
      '- 先查看 `Frontend changed-only Lint` 的原始输出，判断是实际 lint 失败，还是受限环境边界导致的子进程失败。',
      '- 若输出包含受限环境边界提示，优先改到允许子进程的环境执行，而不是先改 lint 规则。',
    ];
  }

  if (failedPhase === 'baseline-quick') {
    return [
      '- 先查看 `npm run validate:baseline:quick` 的原始输出，按类型检查 / 测试 / 权限扫描 / contract 自校验等子项继续分诊。',
      '- 如需进一步分诊，优先查看 `Docs/guide/repo-quality-troubleshooting.md`。',
    ];
  }

  if (failedPhase === 'collect-changed-files') {
    return [
      '- 先查看变更文件收集阶段的原始输出，判断是 `collect-changed-files` 自身失败，还是受限环境边界导致的子进程失败。',
      '- 若输出包含“当前受限环境禁止从 Node 脚本再拉起外部进程”，按“受限环境边界”归类，并改到允许子进程的环境重跑。',
    ];
  }

  if (failedPhase === 'backend-guard') {
    return [
      '- 当前失败已落到后端 / API 专题；先修复 `npm run validate:backend` 的失败项，再重新执行 `npm run validate:ci`。',
      '- 记录或排障时，优先复用 `check:backend-impact` 的命中原因，并按 `dotnet build/test` 输出定位具体项目或测试。',
    ];
  }

  if (failedPhase === 'identity-guard') {
    return [
      '- 当前失败已落到身份语义专题；先修复 `npm run validate:identity` 的失败项，再重新执行 `npm run validate:ci`。',
      '- 记录或排障时，优先复用 `check:identity-impact` 的命中原因与身份语义手册口径。',
    ];
  }

  const lines = [
    '- 当前本地 `Repo Quality` 最小执行面已闭合；如准备发起 `PR -> master`，可把本报告直接回写到批次级回归记录或 PR 描述。',
  ];

  if (backendMatchedFiles.length === 0) {
    lines.push('- 当前未命中后端 / API 影响面；`Backend Guard` 与本地 `validate:ci` 一致地跳过了 `validate:backend`。');
  } else {
    lines.push('- 当前已命中后端 / API 影响面，并已完成 `validate:backend`。');
  }

  if (identityMatchedFiles.length === 0) {
    lines.push('- 当前未命中身份语义影响面；`Identity Guard` 与本地 `validate:ci` 一致地跳过了 `validate:identity`。');
  } else {
    lines.push('- 当前已命中身份语义影响面，并已完成 `validate:identity`；如涉及更深链路，再按专题手册补充人工或协议回归。');
  }

  lines.push('- 如需更强“可合并”结论，可继续执行 `npm run validate:baseline`。');
  return lines;
}

function buildValidateCiMarkdownReport({
  executedAtUtc,
  outcomes,
  changedFiles,
  backendMatchedFiles,
  backendReasonGroups,
  identityMatchedFiles,
  identityReasonGroups,
  failedPhase,
}) {
  const backendOutcome = outcomes.find((outcome) => outcome.phase === 'backend-guard');
  const identityOutcome = outcomes.find((outcome) => outcome.phase === 'identity-guard');
  const findings = getValidateCiFindings(
    failedPhase,
    backendMatchedFiles ?? [],
    identityMatchedFiles ?? []
  );
  const nextStage = failedPhase
    ? 'fix-and-rerun-validate-ci'
    : 'ready-for-pr-batch-record';
  const failedOutcome = failedPhase
    ? outcomes.find((outcome) => outcome.phase === failedPhase) ?? null
    : null;

  return buildSummaryActionReport({
    title: 'Repo Quality Local Validation',
    summaryLines: [
      `- Time: ${executedAtUtc}`,
      `- Overall: ${failedPhase ? 'failed' : 'passed'}`,
      `- Mode: repo-quality-local`,
      ...outcomes.map((outcome) => buildStepSummaryLine(outcome)),
      `- ChangedFiles: ${formatCount(changedFiles?.length)}`,
      `- BackendImpactMatches: ${formatCount(backendMatchedFiles?.length)}`,
      `- BackendReasonGroups: ${formatCount(backendReasonGroups?.length)}`,
      `- BackendGuardMode: ${getConditionalGuardMode(backendMatchedFiles)}`,
      `- IdentityImpactMatches: ${formatCount(identityMatchedFiles?.length)}`,
      `- IdentityReasonGroups: ${formatCount(identityReasonGroups?.length)}`,
      `- IdentityGuardMode: ${getConditionalGuardMode(identityMatchedFiles)}`,
      ...(failedOutcome
        ? [
            `- FailedPhase: ${failedPhase}`,
            `- FailedStep: ${failedOutcome.title}`,
          ]
        : failedPhase
          ? [`- FailedPhase: ${failedPhase}`]
          : []),
      ...(backendOutcome
        ? [`- BackendGuardStatus: ${backendOutcome.ok ? 'passed' : `failed (${backendOutcome.status})`}`]
        : []),
      ...(identityOutcome
        ? [`- IdentityGuardStatus: ${identityOutcome.ok ? 'passed' : `failed (${identityOutcome.status})`}`]
        : []),
      ...buildTriageSummaryLines({
        route: 'repo-quality-local',
        findings,
        nextStage,
      }),
    ],
    actionLines: getValidateCiActionLines(
      failedPhase,
      backendMatchedFiles ?? [],
      identityMatchedFiles ?? []
    ),
  });
}

if (hasFlag('--help') || hasFlag('-h')) {
  printUsage();
  process.exit(0);
}

const reportFile = getArgValue('--report-file', '');
const showReport = hasFlag('--report') || reportFile.length > 0;
const executedAtUtc = new Date().toISOString();
const outcomes = [];

console.log('[validate:ci] 模式：repo-quality-local');

for (const step of REPO_QUALITY_LOCAL_STEPS) {
  const outcome = {
    phase: step.checkName === 'Repo Hygiene'
      ? 'repo-hygiene'
      : step.checkName === 'Frontend Lint'
        ? 'frontend-lint'
        : 'baseline-quick',
    ...runNpm(step.title, step.npmArgs),
  };
  outcomes.push(outcome);

  if (!outcome.ok) {
    if (outcome.error) {
      console.error(`[validate:ci] ${step.title} 执行失败：${outcome.error.message}`);
    }

    if (showReport) {
      const markdownReport = buildValidateCiMarkdownReport({
        executedAtUtc,
        outcomes,
        changedFiles: null,
        backendMatchedFiles: null,
        backendReasonGroups: null,
        identityMatchedFiles: null,
        identityReasonGroups: null,
        failedPhase: outcome.phase,
      });
      printSummaryActionReport('validate:ci', markdownReport);
      if (reportFile) {
        await writeSummaryActionReport('validate:ci', reportFile, markdownReport);
      }
    }

    process.exit(outcome.status);
  }
}

const changedFilesResult = runNode(['Scripts/collect-changed-files.mjs'], { captureStdout: true });
if (!changedFilesResult.ok) {
  if (changedFilesResult.error) {
    console.error(`[validate:ci] 收集变更文件失败：${changedFilesResult.error.message}`);
  }

  if (showReport) {
    const markdownReport = buildValidateCiMarkdownReport({
      executedAtUtc,
      outcomes,
      changedFiles: null,
      backendMatchedFiles: null,
      backendReasonGroups: null,
      identityMatchedFiles: null,
      identityReasonGroups: null,
      failedPhase: 'collect-changed-files',
    });
    printSummaryActionReport('validate:ci', markdownReport);
    if (reportFile) {
      await writeSummaryActionReport('validate:ci', reportFile, markdownReport);
    }
  }

  process.exit(changedFilesResult.status);
}

const changedFiles = splitZeroTerminated(changedFilesResult.stdout);
const backendMatchedFiles = collectBackendImpactMatches(changedFiles);
const backendImpactDetails = collectBackendImpactDetails(changedFiles);
const backendReasonGroups = collectBackendImpactReasonGroups(changedFiles);
const identityMatchedFiles = collectIdentityImpactMatches(changedFiles);
const identityImpactDetails = collectIdentityImpactDetails(changedFiles);
const identityReasonGroups = collectIdentityImpactReasonGroups(changedFiles);

console.log(`\n[validate:ci] ${BACKEND_GUARD_CHECK_NAME} changed-only 判定`);
console.log(`- 当前变更文件：${changedFiles.length} 个`);
console.log(`- 命中后端 / API 影响面：${backendMatchedFiles.length} 个`);
console.log(`- 命中原因类别：${backendReasonGroups.length} 类`);

if (backendMatchedFiles.length === 0) {
  console.log('- 结果：跳过 `validate:backend`，与当前 Repo Quality / Backend Guard 一致。');
} else {
  for (const reasonGroup of backendReasonGroups) {
    console.log(`  - ${reasonGroup.label}：${reasonGroup.files.length} 个文件`);
  }

  console.log('- 命中文件明细：');
  for (const detail of backendImpactDetails) {
    const labels = detail.reasons.map((reason) => reason.label).join(' / ');
    console.log(`  - ${detail.file} [${labels}]`);
  }

  const backendOutcome = {
    phase: 'backend-guard',
    ...runNpm('Backend API Validation', BACKEND_GUARD_VALIDATE_ARGS),
  };
  outcomes.push(backendOutcome);

  if (!backendOutcome.ok) {
    if (backendOutcome.error) {
      console.error(`[validate:ci] Backend API Validation 执行失败：${backendOutcome.error.message}`);
    }

    if (showReport) {
      const markdownReport = buildValidateCiMarkdownReport({
        executedAtUtc,
        outcomes,
        changedFiles,
        backendMatchedFiles,
        backendReasonGroups,
        identityMatchedFiles,
        identityReasonGroups,
        failedPhase: backendOutcome.phase,
      });
      printSummaryActionReport('validate:ci', markdownReport);
      if (reportFile) {
        await writeSummaryActionReport('validate:ci', reportFile, markdownReport);
      }
    }

    process.exit(backendOutcome.status);
  }
}

console.log(`\n[validate:ci] ${IDENTITY_GUARD_CHECK_NAME} changed-only 判定`);
console.log(`- 当前变更文件：${changedFiles.length} 个`);
console.log(`- 命中身份语义影响面：${identityMatchedFiles.length} 个`);
console.log(`- 命中原因类别：${identityReasonGroups.length} 类`);

if (identityMatchedFiles.length === 0) {
  console.log('- 结果：跳过 `validate:identity`，与当前 Repo Quality / Identity Guard 一致。');
} else {
  for (const reasonGroup of identityReasonGroups) {
    console.log(`  - ${reasonGroup.label}：${reasonGroup.files.length} 个文件`);
  }

  console.log('- 命中文件明细：');
  for (const detail of identityImpactDetails) {
    const labels = detail.reasons.map((reason) => reason.label).join(' / ');
    console.log(`  - ${detail.file} [${labels}]`);
  }

  const identityOutcome = {
    phase: 'identity-guard',
    ...runNpm('Identity Regression Validation', IDENTITY_GUARD_VALIDATE_ARGS),
  };
  outcomes.push(identityOutcome);

  if (!identityOutcome.ok) {
    if (identityOutcome.error) {
      console.error(`[validate:ci] Identity Regression Validation 执行失败：${identityOutcome.error.message}`);
    }

    if (showReport) {
      const markdownReport = buildValidateCiMarkdownReport({
        executedAtUtc,
        outcomes,
        changedFiles,
        backendMatchedFiles,
        backendReasonGroups,
        identityMatchedFiles,
        identityReasonGroups,
        failedPhase: identityOutcome.phase,
      });
      printSummaryActionReport('validate:ci', markdownReport);
      if (reportFile) {
        await writeSummaryActionReport('validate:ci', reportFile, markdownReport);
      }
    }

    process.exit(identityOutcome.status);
  }
}

if (showReport) {
  const markdownReport = buildValidateCiMarkdownReport({
    executedAtUtc,
    outcomes,
    changedFiles,
    backendMatchedFiles,
    backendReasonGroups,
    identityMatchedFiles,
    identityReasonGroups,
    failedPhase: null,
  });
  printSummaryActionReport('validate:ci', markdownReport);
  if (reportFile) {
    await writeSummaryActionReport('validate:ci', reportFile, markdownReport);
  }
}
