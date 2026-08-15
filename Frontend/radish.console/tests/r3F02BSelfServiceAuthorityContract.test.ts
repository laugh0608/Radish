import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';

const readConsoleSource = (relativePath: string) => readFileSync(
  new URL(`../${relativePath}`, import.meta.url),
  'utf8',
);

test('R3-F02-B Settings 首次失败不得伪造默认值且 stale 冻结权威写入', () => {
  const source = readConsoleSource('src/pages/Settings/Settings.tsx');

  assert.match(source, /type AuthorityState = 'loading' \| 'ready' \| 'unavailable' \| 'stale'/);
  assert.match(source, /setAuthorityState\(hadSnapshot \? 'stale' : 'unavailable'\)/);
  assert.match(source, /const writesAreAuthoritative = authorityState === 'ready'/);
  assert.match(source, /useUnsavedChangesGuard\(hasUnsavedChanges/);
  assert.match(source, /const \[saving, setSaving\]/);
  assert.match(source, /const \[resetting, setResetting\]/);
  assert.match(source, /const \[passwordBusy, setPasswordBusy\]/);
  assert.doesNotMatch(source, /DEFAULT_SETTINGS|emailNotifications|browserNotifications|twoFactorAuth|sessionTimeout/);
  assert.doesNotMatch(source, /CONSOLE_PERMISSIONS|RequireConsolePermission|requiredPermission/);
});

test('R3-F02-B Console Profile 保留旧快照、精确 dirty 并移除伪最近登录记录', () => {
  const source = readConsoleSource('src/pages/UserProfile/UserProfile.tsx');
  const styles = readConsoleSource('src/pages/UserProfile/UserProfile.css');

  assert.match(source, /loadRequestIdRef/);
  assert.match(source, /setAuthorityState\(hadSnapshot \? 'stale' : 'unavailable'\)/);
  assert.match(source, /isProfileDraftDirty\(values, profileRef\.current\)/);
  assert.match(source, /useUnsavedChangesGuard\(dirty/);
  assert.match(source, /disabled=\{!writesAreAuthoritative \|\| profileBusy\}/);
  assert.match(source, /profile\.account\.unavailable/);
  assert.doesNotMatch(source, /profile\.account\.noLoginRecord/);
  assert.match(styles, /\.user-profile-primary-task[\s\S]*order: -1/);
});

test('R3-F02-B 未保存变更守卫同时覆盖站内导航与浏览器关闭', () => {
  const source = readConsoleSource('src/hooks/useUnsavedChangesGuard.ts');

  assert.match(source, /useBlocker\(locked\)/);
  assert.match(source, /blocker\.proceed\(\)/);
  assert.match(source, /blocker\.reset\(\)/);
  assert.match(source, /window\.addEventListener\('beforeunload'/);
});
