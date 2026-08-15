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
  assert.equal(themeDefinitions.default.themeConfig.token.colorPrimary, '#435c74');
  assert.equal(themeDefinitions.guofeng.themeConfig.token.colorPrimary, '#435c74');
  assert.equal(themeDefinitions.guofeng.themeConfig.token.colorPrimaryHover, '#55738f');
  assert.equal(themeDefinitions.guofeng.themeConfig.token.colorLink, '#435c74');
  assert.equal(themeDefinitions['theme-dark-night'].themeConfig.token.colorPrimary, '#8bb9ca');
  assert.equal(themeDefinitions['theme-sakura'].themeConfig.token.colorPrimary, '#596f88');
  assert.equal(themeDefinitions['theme-sakura'].themeConfig.token.colorLinkHover, '#6c839d');
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

  assert.match(familyTokens, /family-ui v26\.7\.3/);
  assert.match(familyTokens, /--rd-text-on-brand:\s*#fffdf8/);
  assert.match(familyTokens, /--rd-brand-primary:\s*#5d6c57/);
  assert.match(familyTokens, /--rd-state-warning:\s*#b5826d/);
  assert.match(clientTokens, /--theme-bg-app:\s*var\(--rd-bg-app\)/);
  assert.match(clientTokens, /--theme-text-on-brand:\s*var\(--rd-text-on-brand\)/);
  assert.match(clientTokens, /--theme-text-on-accent:\s*var\(--rd-text-on-accent\)/);
  assert.match(clientTokens, /--theme-text-inverse:\s*var\(--theme-text-on-accent\)/);
  assert.match(clientTokens, /--theme-brand-hover:\s*var\(--rd-brand-hover\)/);
  assert.match(clientTokens, /--theme-action-hover:\s*var\(--rd-action-hover\)/);
  assert.match(clientTokens, /--theme-link-primary:\s*var\(--theme-action-primary\)/);
  assert.match(clientTokens, /--theme-link-hover:\s*var\(--theme-action-hover\)/);
  assert.match(clientTokens, /--theme-state-danger:\s*var\(--rd-state-danger\)/);
  assert.match(
    clientTokens,
    /:root,\s*:root\[data-theme='guofeng'\]\s*\{[\s\S]*?--rd-text-on-brand:\s*#fffdf8;[\s\S]*?--rd-brand-primary:\s*#5d6c57;[\s\S]*?--rd-brand-hover:\s*#6e736d;[\s\S]*?--rd-brand-soft:\s*#e2e6de;/,
  );
  assert.match(
    clientTokens,
    /:root\[data-theme='theme-dark-night'\]\s*\{[\s\S]*?--rd-text-on-brand:\s*#0f171d;/,
  );

  const themeSwitcher = readFileSync(
    resolve(clientRoot, 'src/theme/ThemeSwitcher.module.css'),
    'utf8',
  );
  assert.match(
    themeSwitcher,
    /\.swatch\[data-theme-preview='guofeng'\][\s\S]*?linear-gradient\(135deg, #5d6c57/,
  );
  assert.doesNotMatch(clientTokens, /#b24057|#cd5076|rgba\(178, 64, 87/);
  assert.doesNotMatch(themeSwitcher, /#b24057/);
});
