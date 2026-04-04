import { spawnSync } from 'node:child_process';
import process from 'node:process';

const args = new Set(process.argv.slice(2));
const repoRoot = process.cwd();

function hasFlag(flag) {
  return args.has(flag);
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
    const result = spawnSync(command, ['-NoLogo', '-NoProfile', '-Command', '$PSVersionTable.PSVersion.ToString()'], {
      cwd: repoRoot,
      encoding: 'utf8',
      stdio: 'pipe',
      shell: false,
    });

    if (result.status === 0) {
      return command;
    }
  }

  return null;
}

function runStep(title, command, commandArgs) {
  console.log(`\n[baseline] ${title}`);
  console.log(`> ${command} ${commandArgs.join(' ')}`);

  const needsCmdWrapper =
    process.platform === 'win32' && command === 'npm';
  const spawnCommand = needsCmdWrapper ? 'cmd.exe' : command;
  const spawnArgs =
    needsCmdWrapper
      ? ['/d', '/s', '/c', [command, ...commandArgs].join(' ')]
      : commandArgs;

  const result = spawnSync(spawnCommand, spawnArgs, {
    cwd: repoRoot,
    stdio: 'inherit',
    shell: false,
  });

  if (result.error) {
    console.error(`[baseline] ${title} 执行失败：${result.error.message}`);
    process.exit(1);
  }

  if (result.status !== 0) {
    process.exit(result.status ?? 1);
  }
}

function printUsage() {
  console.log('用法: node Scripts/validate-baseline.mjs [--quick] [--with-host-checks]');
  console.log('');
  console.log('--quick             只执行前端 type-check、client node --test、权限扫描');
  console.log('--with-host-checks  在 full 模式下追加 DbMigrate doctor / verify 只读自检');
}

if (hasFlag('--help') || hasFlag('-h')) {
  printUsage();
  process.exit(0);
}

const isQuick = hasFlag('--quick');
const withHostChecks = hasFlag('--with-host-checks');
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
    command: npmCommand,
    args: ['run', 'type-check'],
  },
  {
    title: 'radish.client 最小 node 测试',
    command: npmCommand,
    args: ['run', 'test', '--workspace=radish.client'],
  },
  {
    title: 'Console 权限链路扫描',
    command: npmCommand,
    args: ['run', 'check:console-permissions'],
  },
  {
    title: '身份语义防回归扫描',
    command: npmCommand,
    args: ['run', 'check:identity-claims'],
  },
];

if (!isQuick) {
  steps.push(
    {
      title: '后端解决方案构建',
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

for (const step of steps) {
  runStep(step.title, step.command, step.args);
}

console.log('\n[baseline] 自动化基线验证已完成。');
if (!withHostChecks) {
  console.log('[baseline] 如需宿主只读自检，可追加 --with-host-checks。');
}
console.log('[baseline] 身份语义相关改动建议再补 `npm run validate:identity`。');
console.log('[baseline] HttpTest 仍需在本地服务准备完成后按专题手工执行。');
