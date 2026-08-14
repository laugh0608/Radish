import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

const testDirectory = path.dirname(fileURLToPath(import.meta.url));
const buttonSource = fs.readFileSync(
  path.resolve(testDirectory, '../src/components/Button/Button.tsx'),
  'utf8',
);

test('共享按钮默认使用 button 类型并保留显式 submit 覆盖能力', () => {
  assert.match(buttonSource, /type = 'button'/);
  assert.match(buttonSource, /<button type=\{type\}/);
});
