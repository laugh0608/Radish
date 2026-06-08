import process from 'node:process';

import { createDotNetCommand } from './dotnet-command.mjs';
import { formatCommand, runCommand } from './process-runner.mjs';

const repoRoot = process.cwd();

function runStep(title, command, commandArgs) {
  console.log(`\n[backend] ${title}`);
  console.log(`> ${formatCommand(command, commandArgs)}`);

  const result = runCommand(command, commandArgs, {
    cwd: repoRoot,
    stdio: 'inherit',
  });

  if (result.error) {
    console.error(`[backend] ${title} 执行失败：${result.error.message}`);
    process.exit(1);
  }

  if (result.status !== 0) {
    process.exit(result.status ?? 1);
  }
}

const backendBuild = createDotNetCommand([
  'build',
  'Radish.slnx',
  '-c',
  'Debug',
], { cwd: repoRoot });

const backendTest = createDotNetCommand([
  'test',
  'Radish.Api.Tests',
], { cwd: repoRoot });

const steps = [
  {
    title: '后端解决方案构建',
    command: backendBuild.command,
    args: backendBuild.args,
  },
  {
    title: '后端测试',
    command: backendTest.command,
    args: backendTest.args,
  },
];

console.log('[backend] 模式：backend-regression');

for (const step of steps) {
  runStep(step.title, step.command, step.args);
}

console.log('\n[backend] 后端 / API 专题验证已完成。');
