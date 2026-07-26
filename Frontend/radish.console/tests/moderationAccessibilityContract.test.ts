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
