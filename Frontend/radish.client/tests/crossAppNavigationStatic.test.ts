import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

const testDir = dirname(fileURLToPath(import.meta.url));
const clientRoot = resolve(testDir, '..');

function readClientSource(relativePath: string): string {
  return readFileSync(resolve(clientRoot, relativePath), 'utf8');
}

test('Workbench 进入 Console 应携带来源并保留真实 href', () => {
  const source = readClientSource('src/workbench/WorkbenchApp.tsx');

  assert.match(source, /resolveConsoleExternalUrl\('\/workbench'\)/);
  assert.match(source, /href=\{item\.href\}/);
  assert.match(source, /window\.location\.assign\(href\)/);
  assert.match(source, /workbench\.crossApp\.pending/);
});

test('WebOS 外部应用新标签页应切断 opener', () => {
  const source = readClientSource('src/stores/windowStore.ts');

  assert.match(source, /window\.open\(app\.externalUrl, '_blank', 'noopener,noreferrer'\)/);
});
