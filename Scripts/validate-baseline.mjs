import process from 'node:process';

import {
  buildSummaryActionReport,
  buildTriageSummaryLines,
  printSummaryActionReport,
  writeSummaryActionReport,
} from './m14-reporting.mjs';
import { formatCommand, runCommand } from './process-runner.mjs';

const rawArgs = process.argv.slice(2);
const args = new Set(rawArgs);
const repoRoot = process.cwd();

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

function resolveNpmCommand() {
  return 'npm';
}

function resolvePowerShellCommand() {
  if (process.platform === 'win32') {
    return 'powershell';
  }

  const preferred = ['pwsh'];

  for (const command of preferred) {
    const result = runCommand(command, ['-NoLogo', '-NoProfile', '-Command', '$PSVersionTable.PSVersion.ToString()'], {
      cwd: repoRoot,
      encoding: 'utf8',
      stdio: 'pipe',
    });

    if (result.status === 0) {
      return command;
    }
  }

  return null;
}

function printHostValidationGuidance(failedStep) {
  if (!failedStep) {
    return;
  }

  if (failedStep.phase === 'doctor') {
    console.error('[baseline] host-checks 分流：当前失败落在 `DbMigrate doctor`。');
    console.error('[baseline] 下一步：先修配置、连接定义和关键 ConnId，再重新执行 `npm run validate:baseline:host`。');
    console.error('[baseline] 当前不建议直接进入 `npm run check:host-runtime`，因为启动前前提尚未成立。');
    return;
  }

  if (failedStep.phase === 'verify') {
    console.error('[baseline] host-checks 分流：当前失败落在 `DbMigrate verify`。');
    console.error('[baseline] 下一步：先修数据库前置、缺列/表结构或种子状态，再重新执行 `npm run validate:baseline:host`。');
    console.error('[baseline] 当前不建议直接进入 `npm run check:host-runtime`，因为宿主最小运行前提尚未闭合。');
    return;
  }

  console.error('[baseline] host-checks 分流：当前失败发生在默认基线阶段。');
  console.error('[baseline] 下一步：先按 type-check/build/test/扫描类失败修正仓库回归，再回到 `npm run validate:baseline:host`。');
  console.error('[baseline] 只有默认基线与 doctor/verify 都通过后，才建议进入运行态检查 `npm run check:host-runtime`。');
}

function getHostValidationGuidanceLines(failedStep) {
  if (!failedStep) {
    return [];
  }

  if (failedStep.phase === 'doctor') {
    return [
      '- 先修配置、连接定义和关键 ConnId，再重新执行 `npm run validate:baseline:host`。',
      '- 当前不建议直接进入 `npm run check:host-runtime`，因为启动前前提尚未成立。',
    ];
  }

  if (failedStep.phase === 'verify') {
    return [
      '- 先修数据库前置、缺列/表结构或种子状态，再重新执行 `npm run validate:baseline:host`。',
      '- 当前不建议直接进入 `npm run check:host-runtime`，因为宿主最小运行前提尚未闭合。',
    ];
  }

  return [
    '- 先按 type-check/build/test/扫描类失败修正仓库回归，再回到 `npm run validate:baseline:host`。',
    '- 只有默认基线与 doctor/verify 都通过后，才建议进入运行态检查 `npm run check:host-runtime`。',
  ];
}

function runStep(step) {
  const { title, command, args: commandArgs } = step;
  console.log(`\n[baseline] ${title}`);
  console.log(`> ${formatCommand(command, commandArgs)}`);

  const result = runCommand(command, commandArgs, {
    cwd: repoRoot,
    stdio: 'inherit',
  });

  if (result.error) {
    console.error(`[baseline] ${title} 执行失败：${result.error.message}`);
    return {
      ok: false,
      status: 1,
      step,
    };
  }

  if (result.status !== 0) {
    return {
      ok: false,
      status: result.status ?? 1,
      step,
    };
  }

  return {
    ok: true,
    status: 0,
    step,
  };
}

function printUsage() {
  console.log('用法: node Scripts/validate-baseline.mjs [--quick] [--with-host-checks] [--report] [--report-file <path>]');
  console.log('');
  console.log('--quick             只执行前端 type-check、client node --test、权限扫描与身份语义轻量校验');
  console.log('--with-host-checks  在 full 模式下追加 DbMigrate doctor / verify 只读自检');
  console.log('--report            输出可直接回写到记录/PR 的固定 Markdown 报告');
  console.log('--report-file       将 Markdown 报告直接写入指定文件（会自动启用 --report）');
}

function buildStepSummaryLine(outcome) {
  if (outcome.ok) {
    return `- ${outcome.step.title}: 正常`;
  }

  return `- ${outcome.step.title}: 失败 (${outcome.status})`;
}

function buildBaselineActionLines({ isQuick, withHostChecks, failedStep }) {
  if (failedStep) {
    if (withHostChecks && !isQuick) {
      return getHostValidationGuidanceLines(failedStep);
    }

    return [
      '- 先修复当前默认基线失败项，再重新执行对应的 `validate:baseline` 入口。',
    ];
  }

  const lines = [];
  if (!withHostChecks) {
    lines.push('- 如需补齐宿主启动前前提验证，继续执行 `npm run validate:baseline:host`。');
  } else if (!isQuick) {
    lines.push('- 当前启动前基线、doctor 与 verify 均已闭合；宿主启动后执行 `npm run check:host-runtime`。');
    lines.push('- 若需要更完整分诊，可追加 `npm run check:host-runtime -- --details --report`。');
  } else {
    lines.push('- 当前仅完成 quick 模式验证；如需宿主主线闭环，继续执行 `npm run validate:baseline:host`。');
  }

  lines.push('- 身份语义相关改动建议再补 `npm run validate:identity`。');
  lines.push('- HttpTest 仍需在本地服务准备完成后按专题手工执行。');
  return lines;
}

function getBaselineTriageFindings(failedStep) {
  if (!failedStep) {
    return [];
  }

  if (failedStep.phase === 'doctor') {
    return [
      {
        scope: 'doctor',
        code: 'config-precheck',
      },
    ];
  }

  if (failedStep.phase === 'verify') {
    return [
      {
        scope: 'verify',
        code: 'database-precheck',
      },
    ];
  }

  return [
    {
      scope: 'baseline',
      code: 'baseline-regression',
    },
  ];
}

function getBaselineNextStage({ isQuick, withHostChecks, failedStep }) {
  if (failedStep) {
    if (failedStep.phase === 'doctor') {
      return 'fix-config-and-rerun-preflight';
    }

    if (failedStep.phase === 'verify') {
      return 'fix-database-and-rerun-preflight';
    }

    return 'fix-baseline-and-rerun-preflight';
  }

  if (withHostChecks && !isQuick) {
    return 'run-runtime-check';
  }

  if (isQuick) {
    return 'upgrade-to-host-preflight';
  }

  return 'consider-host-preflight';
}

function buildBaselineMarkdownReport({
  executedAtUtc,
  isQuick,
  withHostChecks,
  outcomes,
  failedStep,
}) {
  const overallStatus = failedStep ? 'failed' : 'passed';
  const mode = isQuick ? 'quick' : 'full';
  const hostChecks = withHostChecks && !isQuick ? 'enabled' : 'disabled';
  const triageFindings = getBaselineTriageFindings(failedStep);

  return buildSummaryActionReport({
    title: 'Baseline Validation',
    summaryLines: [
      `- Time: ${executedAtUtc}`,
      `- Overall: ${overallStatus}`,
      `- Mode: ${mode}`,
      `- HostChecks: ${hostChecks}`,
      ...outcomes.map((outcome) => buildStepSummaryLine(outcome)),
      ...(failedStep
        ? [
            `- FailedPhase: ${failedStep.phase}`,
            `- FailedStep: ${failedStep.title}`,
          ]
        : []),
      ...buildTriageSummaryLines({
        route: 'preflight',
        findings: triageFindings,
        nextStage: getBaselineNextStage({
          isQuick,
          withHostChecks,
          failedStep,
        }),
      }),
    ],
    actionLines: buildBaselineActionLines({
      isQuick,
      withHostChecks,
      failedStep,
    }),
  });
}

if (hasFlag('--help') || hasFlag('-h')) {
  printUsage();
  process.exit(0);
}

const isQuick = hasFlag('--quick');
const withHostChecks = hasFlag('--with-host-checks');
const reportFile = getArgValue('--report-file', '');
const showReport = hasFlag('--report') || reportFile.length > 0;
const npmCommand = resolveNpmCommand();
const needsPowerShell = !isQuick || withHostChecks;
const powerShellCommand = needsPowerShell ? resolvePowerShellCommand() : null;

if (needsPowerShell && !powerShellCommand) {
  console.error('[baseline] 未找到可用的 PowerShell (`pwsh` 或 `powershell`)。');
  process.exit(1);
}

const steps = [
  {
    title: '前端 TypeScript 类型检查',
    phase: 'baseline',
    command: npmCommand,
    args: ['run', 'type-check'],
  },
  {
    title: 'radish.client 最小 node 测试',
    phase: 'baseline',
    command: npmCommand,
    args: ['run', 'test', '--workspace=radish.client'],
  },
  {
    title: 'Console 权限链路扫描',
    phase: 'baseline',
    command: npmCommand,
    args: ['run', 'check:console-permissions'],
  },
  {
    title: 'Repo Quality contract 自校验',
    phase: 'baseline',
    command: npmCommand,
    args: ['run', 'check:repo-quality-contract'],
  },
  {
    title: '身份语义影响面判定自校验',
    phase: 'baseline',
    command: npmCommand,
    args: ['run', 'check:identity-impact:self-test'],
  },
  {
    title: '身份语义防回归扫描',
    phase: 'baseline',
    command: npmCommand,
    args: ['run', 'check:identity-claims'],
  },
];

if (!isQuick) {
  steps.push(
    {
      title: '后端解决方案构建',
      phase: 'baseline',
      command: powerShellCommand,
      args: [
        '-ExecutionPolicy',
        'Bypass',
        '-File',
        'Scripts/dotnet-local.ps1',
        'build',
        'Radish.slnx',
        '-c',
        'Debug',
      ],
    },
    {
      title: '后端测试',
      phase: 'baseline',
      command: powerShellCommand,
      args: [
        '-ExecutionPolicy',
        'Bypass',
        '-File',
        'Scripts/dotnet-local.ps1',
        'test',
        'Radish.Api.Tests',
      ],
    }
  );
}

if (!isQuick && withHostChecks) {
  steps.push(
    {
      title: 'DbMigrate doctor 只读自检',
      phase: 'doctor',
      command: powerShellCommand,
      args: [
        '-ExecutionPolicy',
        'Bypass',
        '-Command',
        '& ./Scripts/dotnet-local.ps1 run --no-build --project Radish.DbMigrate/Radish.DbMigrate.csproj -- doctor',
      ],
    },
    {
      title: 'DbMigrate verify 只读自检',
      phase: 'verify',
      command: powerShellCommand,
      args: [
        '-ExecutionPolicy',
        'Bypass',
        '-Command',
        '& ./Scripts/dotnet-local.ps1 run --no-build --project Radish.DbMigrate/Radish.DbMigrate.csproj -- verify',
      ],
    }
  );
}

console.log(`[baseline] 模式：${isQuick ? 'quick' : 'full'}${withHostChecks && !isQuick ? ' + host-checks' : ''}`);

const outcomes = [];
const executedAtUtc = new Date().toISOString();
for (const step of steps) {
  const outcome = runStep(step);
  outcomes.push(outcome);
  if (!outcome.ok) {
    if (withHostChecks && !isQuick) {
      printHostValidationGuidance(step);
    }
    if (showReport) {
      const markdownReport = buildBaselineMarkdownReport({
        executedAtUtc,
        isQuick,
        withHostChecks,
        outcomes,
        failedStep: step,
      });
      printSummaryActionReport('baseline', markdownReport);
      if (reportFile) {
        await writeSummaryActionReport('baseline', reportFile, markdownReport);
      }
    }
    process.exit(outcome.status);
  }
}

console.log('\n[baseline] 自动化基线验证已完成。');
if (!withHostChecks) {
  console.log('[baseline] 如需宿主只读自检，可追加 --with-host-checks。');
} else if (!isQuick) {
  console.log('[baseline] host-checks 已通过：当前启动前基线、doctor 与 verify 均已闭合。');
  console.log('[baseline] 下一步：如果宿主已经启动或准备启动后做运行态复核，执行 `npm run check:host-runtime`。');
  console.log('[baseline] 若需要更完整分诊，可追加 `npm run check:host-runtime -- --details --report`。');
}
console.log('[baseline] 身份语义相关改动建议再补 `npm run validate:identity`。');
console.log('[baseline] HttpTest 仍需在本地服务准备完成后按专题手工执行。');

if (showReport) {
  const markdownReport = buildBaselineMarkdownReport({
    executedAtUtc,
    isQuick,
    withHostChecks,
    outcomes,
    failedStep: null,
  });
  printSummaryActionReport('baseline', markdownReport);
  if (reportFile) {
    await writeSummaryActionReport('baseline', reportFile, markdownReport);
  }
}
