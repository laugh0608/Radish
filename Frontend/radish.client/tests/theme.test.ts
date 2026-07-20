import assert from 'node:assert/strict';
import { globSync, readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';
import {
  DEFAULT_THEME,
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
});

test('主题 ID 校验应拒绝未注册资源并回退到默认内建主题', () => {
  assert.equal(isThemeId('theme-sakura'), true);
  assert.equal(isEntitlementThemeId('theme-sakura'), true);
  assert.equal(isBuiltinThemeId('theme-sakura'), false);
  assert.equal(isThemeId('theme-retired'), false);
  assert.equal(resolveTheme('theme-retired'), DEFAULT_THEME);
  assert.equal(resolveBuiltinTheme('theme-dark-night'), DEFAULT_THEME);
});

test('可主题化 CSS 混色必须使用语义表面高光而不是硬编码白色', () => {
  const cssFiles = globSync('src/**/*.css', { cwd: clientRoot });

  for (const cssFile of cssFiles) {
    const source = readFileSync(resolve(clientRoot, cssFile), 'utf8');
    assert.doesNotMatch(source, /color-mix\([^\n]*\bwhite\b/, cssFile);
  }
});
