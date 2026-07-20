import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

const uiRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..');

test('ReactionBar 只读态应同时阻断气泡、快捷面板与 sticker 入口', () => {
  const source = readFileSync(resolve(uiRoot, 'src/components/ReactionBar/ReactionBar.tsx'), 'utf8');

  assert.match(source, /if \(readOnly \|\| loading\) \{/);
  assert.match(source, /pickerOpen && isLoggedIn && !readOnly/);
  assert.match(source, /disabled=\{readOnly \|\| loading \|\| reachedReactionLimit\}/);
});

test('ReactionBar 应支持显式回应文字并使用宿主主题语义 token', () => {
  const source = readFileSync(resolve(uiRoot, 'src/components/ReactionBar/ReactionBar.tsx'), 'utf8');
  const styles = readFileSync(resolve(uiRoot, 'src/components/ReactionBar/ReactionBar.module.css'), 'utf8');

  assert.match(source, /showAddReactionLabel/);
  assert.match(source, /styles\.plusLabel/);
  assert.match(styles, /var\(--theme-brand-primary/);
  assert.match(styles, /var\(--theme-bg-surface/);
  assert.match(styles, /var\(--theme-text-primary/);
});
