import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import test from 'node:test';

import { collectChangedFiles } from './changed-files.mjs';

function runGit(repoRoot, args) {
  execFileSync('git', args, {
    cwd: repoRoot,
    stdio: 'ignore',
  });
}

function writeFixture(repoRoot, relativePath, content) {
  const filePath = path.join(repoRoot, relativePath);
  fs.mkdirSync(path.dirname(filePath), { recursive: true });
  fs.writeFileSync(filePath, content, 'utf8');
}

function createRepository(t) {
  const repoRoot = fs.mkdtempSync(path.join(os.tmpdir(), 'radish-changed-files-'));
  t.after(() => fs.rmSync(repoRoot, { recursive: true, force: true }));

  runGit(repoRoot, ['init', '--initial-branch=dev']);
  runGit(repoRoot, ['config', 'user.name', 'Radish Test']);
  runGit(repoRoot, ['config', 'user.email', 'radish-test@example.invalid']);

  writeFixture(repoRoot, '.gitignore', 'ignored.txt\n');
  writeFixture(repoRoot, 'staged.md', 'initial\n');
  writeFixture(repoRoot, 'tracked.md', 'initial\n');
  runGit(repoRoot, ['add', '.gitignore', 'staged.md', 'tracked.md']);
  runGit(repoRoot, ['commit', '-m', 'test: initialize fixture']);

  return repoRoot;
}

test('worktree 模式同时收集 staged、unstaged 与未跟踪文件', (t) => {
  const repoRoot = createRepository(t);
  writeFixture(repoRoot, 'staged.md', 'staged change\n');
  runGit(repoRoot, ['add', 'staged.md']);
  writeFixture(repoRoot, 'tracked.md', 'unstaged change\n');
  writeFixture(repoRoot, 'new document.md', 'untracked\n');
  writeFixture(repoRoot, 'ignored.txt', 'ignored\n');

  const files = collectChangedFiles({ repoRoot, mode: 'worktree' }).sort();

  assert.deepEqual(files, ['new document.md', 'staged.md', 'tracked.md']);
});

test('staged 模式不会把未跟踪或仅有 unstaged 修改的文件混入结果', (t) => {
  const repoRoot = createRepository(t);
  writeFixture(repoRoot, 'staged.md', 'staged change\n');
  runGit(repoRoot, ['add', 'staged.md']);
  writeFixture(repoRoot, 'tracked.md', 'unstaged change\n');
  writeFixture(repoRoot, 'new.md', 'untracked\n');

  assert.deepEqual(collectChangedFiles({ repoRoot, mode: 'staged' }), ['staged.md']);
});
