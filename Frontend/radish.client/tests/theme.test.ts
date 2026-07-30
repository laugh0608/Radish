import assert from 'node:assert/strict';
import { globSync, readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';
import {
  DEFAULT_THEME,
  applyTheme,
  builtinThemeIds,
  entitlementThemeIds,
  isBuiltinThemeId,
  isEntitlementThemeId,
  isThemeId,
  resolveBuiltinTheme,
  resolveTheme,
  themeDefinitions,
} from '../src/theme/theme.ts';

const clientRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..');

test('主题注册表应同时包含内建主题与正式权益资源', () => {
  assert.deepEqual(builtinThemeIds, ['default', 'guofeng']);
  assert.deepEqual(entitlementThemeIds, ['theme-dark-night', 'theme-sakura']);
  assert.equal(themeDefinitions['theme-dark-night'].colorScheme, 'dark');
  assert.equal(themeDefinitions['theme-sakura'].access, 'entitlement');
  assert.equal(themeDefinitions.guofeng.themeConfig.token.colorWarning, '#b5826d');
  assert.equal(themeDefinitions.guofeng.themeConfig.token.colorError, '#c3564d');
  assert.equal(themeDefinitions['theme-dark-night'].themeConfig.token.colorInfo, '#8bb9ca');
});

test('主题 ID 校验应拒绝未注册资源并回退到默认内建主题', () => {
  assert.equal(isThemeId('theme-sakura'), true);
  assert.equal(isEntitlementThemeId('theme-sakura'), true);
  assert.equal(isBuiltinThemeId('theme-sakura'), false);
  assert.equal(isThemeId('theme-retired'), false);
  assert.equal(resolveTheme('theme-retired'), DEFAULT_THEME);
  assert.equal(resolveBuiltinTheme('theme-dark-night'), DEFAULT_THEME);
});

test('applyTheme 应从已注册主题派生 family-ui 明暗属性', () => {
  const previousDocument = Object.getOwnPropertyDescriptor(globalThis, 'document');
  const documentElement = {
    dataset: {} as Record<string, string>,
    style: { colorScheme: '' },
  };

  Object.defineProperty(globalThis, 'document', {
    configurable: true,
    value: { documentElement },
  });

  try {
    applyTheme('theme-dark-night');
    assert.equal(documentElement.dataset.theme, 'theme-dark-night');
    assert.equal(documentElement.dataset.rdTheme, 'dark');
    assert.equal(documentElement.style.colorScheme, 'dark');

    applyTheme('guofeng');
    assert.equal(documentElement.dataset.theme, 'guofeng');
    assert.equal(documentElement.dataset.rdTheme, 'light');
    assert.equal(documentElement.style.colorScheme, 'light');
  } finally {
    if (previousDocument) {
      Object.defineProperty(globalThis, 'document', previousDocument);
    } else {
      Reflect.deleteProperty(globalThis, 'document');
    }
  }
});

test('可主题化 CSS 混色必须使用语义表面高光而不是硬编码白色', () => {
  const cssFiles = globSync('src/**/*.css', { cwd: clientRoot });

  for (const cssFile of cssFiles) {
    const source = readFileSync(resolve(clientRoot, cssFile), 'utf8');
    assert.doesNotMatch(source, /color-mix\([^\n]*\bwhite\b/, cssFile);
  }
});

test('family-ui token 副本与 Client L2 应保持可追溯关系', () => {
  const familyTokens = readFileSync(
    resolve(clientRoot, '../radish.ui/src/theme/family-ui-tokens.css'),
    'utf8',
  );
  const clientTokens = readFileSync(resolve(clientRoot, 'src/theme/theme-tokens.css'), 'utf8');

  assert.match(familyTokens, /family-ui v26\.7\.2/);
  assert.match(familyTokens, /--rd-state-warning:\s*#b5826d/);
  assert.match(clientTokens, /--theme-bg-app:\s*var\(--rd-bg-app\)/);
  assert.match(clientTokens, /--theme-state-danger:\s*var\(--rd-state-danger\)/);
});
