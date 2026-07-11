import process from 'node:process';

import { runCommand } from './process-runner.mjs';

function resolvePowerShellCommand(cwd) {
  if (process.platform === 'win32') {
    return 'powershell';
  }

  for (const command of ['pwsh']) {
    const result = runCommand(command, ['-NoLogo', '-NoProfile', '-Command', '$PSVersionTable.PSVersion.ToString()'], {
      cwd,
      encoding: 'utf8',
      stdio: 'pipe',
    });

    if (result.status === 0) {
      return command;
    }
  }

  return null;
}

function appendDotNetLocalArgs(dotnetArgs) {
  const nextArgs = [...dotnetArgs];
  const primaryCommand = String(dotnetArgs[0] ?? '').toLowerCase();

  const hasMaxCpuCountOverride = dotnetArgs.some((arg) =>
    /^(--maxcpucount|-m(?::.*)?|\/m(?::.*)?)$/.test(arg)
  );
  if ((primaryCommand === 'build' || primaryCommand === 'test') && !hasMaxCpuCountOverride) {
    nextArgs.push('-m:1');
  }

  const hasRestoreParallelOverride = dotnetArgs.some((arg) => arg === '--disable-parallel');
  if (primaryCommand === 'restore' && !hasRestoreParallelOverride) {
    nextArgs.push('--disable-parallel');
  }

  return nextArgs;
}

export function createDotNetCommand(dotnetArgs, { cwd, preferPowerShell = true } = {}) {
  if (process.platform === 'win32' && preferPowerShell) {
    const powerShellCommand = resolvePowerShellCommand(cwd);
    if (powerShellCommand) {
      return {
        command: powerShellCommand,
        args: [
          '-ExecutionPolicy',
          'Bypass',
          '-File',
          'Scripts/dotnet-local.ps1',
          ...dotnetArgs,
        ],
      };
    }
  }

  return {
    command: 'dotnet',
    args: appendDotNetLocalArgs(dotnetArgs),
  };
}
