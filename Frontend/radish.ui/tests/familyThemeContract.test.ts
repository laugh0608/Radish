import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';
import { antdDarkTheme, antdTheme, radishColors } from '../src/theme/antd-theme.ts';

const testDirectory = dirname(fileURLToPath(import.meta.url));

test('family-ui token 副本应固定并声明接入版本', () => {
  const css = readFileSync(resolve(testDirectory, '../src/theme/family-ui-tokens.css'), 'utf8');
  const json = JSON.parse(
    readFileSync(resolve(testDirectory, '../src/theme/family-ui-tokens.json'), 'utf8'),
  ) as { meta?: { version?: string } };

  assert.match(css, /family-ui v26\.7\.3/);
  assert.match(css, /--rd-text-on-brand:\s*#fffdf8/);
  assert.match(css, /--rd-brand-primary:\s*#5d6c57/);
  assert.equal(json.meta?.version, 'v26.7.3');
});

test('共享 Ant Design 亮色主题应使用 family-ui Workbench 状态语义', () => {
  assert.equal(radishColors.brand, '#b24057');
  assert.equal(radishColors.primary, '#435c74');
  assert.notEqual(radishColors.brand, radishColors.primary);
  assert.equal(antdTheme.token?.colorPrimary, '#435c74');
  assert.equal(antdTheme.token?.colorSuccess, '#4f9c83');
  assert.equal(antdTheme.token?.colorWarning, '#b5826d');
  assert.equal(antdTheme.token?.colorError, '#c3564d');
  assert.equal(antdTheme.token?.colorInfo, '#435c74');
});

test('共享 Ant Design 暗色主题不应回退到默认黑底和亮色 Modal', () => {
  assert.equal(antdDarkTheme.token?.colorBgBase, '#1a1713');
  assert.equal(antdDarkTheme.token?.colorWarning, '#c9997f');
  assert.equal(antdDarkTheme.components?.Modal?.headerBg, '#23201a');
  assert.equal(antdDarkTheme.components?.Modal?.contentBg, '#23201a');
});
