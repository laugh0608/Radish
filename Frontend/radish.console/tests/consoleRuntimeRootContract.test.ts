import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';

const mainSource = readFileSync(
  new URL('../src/main.tsx', import.meta.url),
  'utf8',
);

test('Console 开发入口应在 HMR 期间复用既有 React Root', () => {
  assert.match(mainSource, /import\.meta\.hot\?\.data\.consoleRoot/);
  assert.match(mainSource, /import\.meta\.hot\.data\.consoleRoot = root/);
  assert.match(mainSource, /root\.render\(/);
});
