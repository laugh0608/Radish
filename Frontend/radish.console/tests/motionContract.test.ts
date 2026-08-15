import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

const consoleRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..');

test('Console 宿主应统一限制硬编码动效并保留静态状态表面', () => {
  const styles = readFileSync(resolve(consoleRoot, 'src/index.css'), 'utf8');

  assert.match(styles, /@media \(prefers-reduced-motion: reduce\)/);
  assert.match(styles, /animation-delay:\s*0ms !important/);
  assert.match(styles, /animation-duration:\s*0\.01ms !important/);
  assert.match(styles, /animation-iteration-count:\s*1 !important/);
  assert.match(styles, /scroll-behavior:\s*auto !important/);
  assert.match(styles, /transition-delay:\s*0ms !important/);
  assert.match(styles, /transition-duration:\s*0\.01ms !important/);
  assert.doesNotMatch(styles, /display:\s*none !important/);
  assert.doesNotMatch(styles, /visibility:\s*hidden !important/);
});
