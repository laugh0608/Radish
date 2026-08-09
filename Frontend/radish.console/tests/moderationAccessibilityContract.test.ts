import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { resolve } from 'node:path';

test('内容治理筛选控件应绑定稳定的双语可访问名称', () => {
  const consoleRoot = resolve(import.meta.dirname, '..');
  const pageSource = readFileSync(
    resolve(consoleRoot, 'src/pages/Moderation/ModerationPage.tsx'),
    'utf8',
  );

  assert.match(pageSource, /aria-label=\{t\('moderation\.case\.filter\.allStatuses'\)\}/);
  assert.match(pageSource, /aria-label=\{t\('moderation\.case\.filter\.targetType'\)\}/);
  assert.match(pageSource, /aria-label=\{t\('moderation\.case\.filter\.keyword'\)\}/);
});

test('内容治理切换、刷新与任务导航应提供可访问名称', () => {
  const consoleRoot = resolve(import.meta.dirname, '..');
  const pageSource = readFileSync(
    resolve(consoleRoot, 'src/pages/Moderation/ModerationPage.tsx'),
    'utf8',
  );
  const appealSource = readFileSync(
    resolve(consoleRoot, 'src/pages/Moderation/ModerationAppealsWorkspace.tsx'),
    'utf8',
  );

  for (const source of [pageSource, appealSource]) {
    assert.match(source, /className="moderation-workspace-switch" aria-label=\{t\('moderation\.workspaceSwitch'\)\}/);
    assert.match(source, /className="moderation-mobile-toolbar"/);
    assert.match(source, /aria-label=\{t\('moderation\.refresh'\)\}/);
    assert.match(source, /className="moderation-case-task-header"/);
  }

  assert.match(pageSource, /aria-label=\{t\('moderation\.case\.backToQueue'\)\}/);
  assert.match(pageSource, /aria-label=\{t\('moderation\.case\.retryDetail'\)\}/);
  assert.match(appealSource, /aria-label=\{t\('moderation\.appeal\.backToQueue'\)\}/);
  assert.match(appealSource, /aria-label=\{t\('moderation\.appeal\.retryDetail'\)\}/);
});
