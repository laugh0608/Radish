import { writeFileSync } from 'node:fs';
import process from 'node:process';

import { collectChangedFiles } from './changed-files.mjs';

const rawArgs = process.argv.slice(2);
const repoRoot = process.cwd();

function readOption(name) {
  const prefix = `${name}=`;
  const matched = rawArgs.find((arg) => arg.startsWith(prefix));
  if (!matched) {
    return null;
  }

  const value = matched.slice(prefix.length).trim();
  return value === '' ? null : value;
}

function hasFlag(flag) {
  return rawArgs.includes(flag);
}

function collectFiles() {
  return collectChangedFiles({
    repoRoot,
    base: readOption('--base'),
    head: readOption('--head'),
    mode: readOption('--mode') ?? 'worktree',
    includeAllOnEmptyBase: hasFlag('--include-all-on-empty-base'),
  });
}

function outputFiles(files) {
  const outputPath = readOption('--write');
  const payload = files.length > 0
    ? `${files.join('\0')}\0`
    : '';

  if (outputPath) {
    writeFileSync(outputPath, payload, 'utf8');
  } else {
    process.stdout.write(payload);
  }
}

function main() {
  let files;
  try {
    files = collectFiles();
  } catch (error) {
    console.error(error instanceof Error ? error.message : String(error));
    process.exit(1);
  }

  outputFiles(files);
}

main();
