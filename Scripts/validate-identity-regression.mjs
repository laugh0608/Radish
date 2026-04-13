import process from 'node:process';

import { formatCommand, runCommand } from './process-runner.mjs';

const repoRoot = process.cwd();
const identityTestFilter = [
  'FullyQualifiedName~Radish.Api.Tests.ClaimsPrincipalNormalizerTests',
  'FullyQualifiedName~Radish.Api.Tests.HttpContextUserTests',
  'FullyQualifiedName~Radish.Api.Tests.Controllers.AccountControllerTest',
  'FullyQualifiedName~Radish.Api.Tests.Controllers.AuthorizationControllerTest',
  'FullyQualifiedName~Radish.Api.Tests.Controllers.UserInfoControllerTest',
].join('|');

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

function runStep(title, command, commandArgs) {
  console.log(`\n[identity] ${title}`);
  console.log(`> ${formatCommand(command, commandArgs)}`);

  const result = runCommand(command, commandArgs, {
    cwd: repoRoot,
    stdio: 'inherit',
  });

  if (result.error) {
    console.error(`[identity] ${title} 执行失败：${result.error.message}`);
    process.exit(1);
  }

  if (result.status !== 0) {
    process.exit(result.status ?? 1);
  }
}

const npmCommand = resolveNpmCommand();
const powerShellCommand = resolvePowerShellCommand();

if (!powerShellCommand) {
  console.error('[identity] 未找到可用的 PowerShell (`pwsh` 或 `powershell`)。');
  process.exit(1);
}

const steps = [
  {
    title: '运行时散点 Claim 读取扫描',
    command: npmCommand,
    args: ['run', 'check:identity-runtime'],
  },
  {
    title: '协议输出回退风险扫描',
    command: npmCommand,
    args: ['run', 'check:identity-protocol-output'],
  },
  {
    title: '身份语义后端定向测试',
    command: powerShellCommand,
    args: [
      '-ExecutionPolicy',
      'Bypass',
      '-File',
      'Scripts/dotnet-local.ps1',
      'test',
      'Radish.Api.Tests',
      '--filter',
      identityTestFilter,
    ],
  },
];

console.log('[identity] 模式：identity-regression');

for (const step of steps) {
  runStep(step.title, step.command, step.args);
}

console.log('\n[identity] 身份语义专题验证已完成。');
console.log('[identity] 若本轮触达 Auth 输出 / userinfo / Token 解析 / 官方消费者，请继续按手册补 `Radish.Api.AuthFlow.http` 与官方顺序回归。');
