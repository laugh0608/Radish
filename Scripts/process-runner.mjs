import { spawnSync } from 'node:child_process';
import process from 'node:process';

function resolveCommand(command) {
  if (command === 'node') {
    return process.execPath;
  }

  if (process.platform === 'win32') {
    if (command === 'npm') {
      return 'npm.cmd';
    }

    if (command === 'npx') {
      return 'npx.cmd';
    }
  }

  return command;
}

function runSpawn(command, args, options) {
  return spawnSync(command, args, {
    shell: false,
    ...options,
  });
}

export function formatCommand(command, args = []) {
  return [command, ...args].join(' ');
}

function normalizeSpawnError(command, args, error) {
  if (!error) {
    return null;
  }

  if (
    process.platform === 'win32' &&
    (error.code === 'EPERM' || error.code === 'EINVAL')
  ) {
    const normalized = new Error(
      `无法启动子进程：${formatCommand(command, args)}。当前受限环境禁止从 Node 脚本再拉起外部进程；请直接在外层 shell 执行对应命令，或在允许子进程的环境中运行。原始错误：${error.message}`
    );
    normalized.code = error.code;
    return normalized;
  }

  return error;
}

export function runCommand(command, args = [], options = {}) {
  let result = runSpawn(resolveCommand(command), args, options);

  if (
    process.platform === 'win32' &&
    command === 'npm' &&
    result.error?.code === 'EINVAL'
  ) {
    result = runSpawn(
      process.env.ComSpec || 'cmd.exe',
      ['/d', '/s', '/c', formatCommand(command, args)],
      options
    );
  }

  if (result.error) {
    result.error = normalizeSpawnError(command, args, result.error);
  }

  return result;
}
