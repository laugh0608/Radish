import process from 'node:process';

import { runCommand } from './process-runner.mjs';

const rawArgs = process.argv.slice(2);
const separatorIndex = rawArgs.indexOf('--');

function readOption(name) {
  const prefix = `${name}=`;
  const matched = rawArgs.find((arg) => arg.startsWith(prefix));
  return matched ? matched.slice(prefix.length).trim() : null;
}

function exitWithResult(result, fallbackCode = 1) {
  if (result.error) {
    console.error(`[run-with-changed-files] 执行失败：${result.error.message}`);
    process.exit(fallbackCode);
  }

  process.exit(result.status ?? fallbackCode);
}

function collectChangedFiles(mode) {
  const args = ['Scripts/collect-changed-files.mjs'];
  if (mode) {
    args.push(`--mode=${mode}`);
  }

  const result = runCommand('node', args, {
    cwd: process.cwd(),
    encoding: null,
    stdio: ['ignore', 'pipe', 'inherit'],
  });

  if (result.status !== 0 || result.error) {
    exitWithResult(result);
  }

  return result.stdout ?? Buffer.alloc(0);
}

function runTarget(command, inputBuffer) {
  const [targetCommand, ...targetArgs] = command;
  const result = runCommand(targetCommand, targetArgs, {
    cwd: process.cwd(),
    input: inputBuffer,
    stdio: ['pipe', 'inherit', 'inherit'],
  });

  if (result.status !== 0 || result.error) {
    exitWithResult(result);
  }
}

function main() {
  if (separatorIndex === -1 || separatorIndex === rawArgs.length - 1) {
    console.error('[run-with-changed-files] 用法：node Scripts/run-with-changed-files.mjs [--mode=worktree|staged|tracked] -- <command...>');
    process.exit(1);
  }

  const mode = readOption('--mode') ?? 'worktree';
  const command = rawArgs.slice(separatorIndex + 1);
  const changedFiles = collectChangedFiles(mode);
  runTarget(command, changedFiles);
}

main();
