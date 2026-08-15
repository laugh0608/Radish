import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { resolveFailedSnapshotState } from '../src/apps/profile/profileAuthority.ts';

const readClientSource = (relativePath: string) => readFileSync(
  new URL(`../${relativePath}`, import.meta.url),
  'utf8',
);

test('R3-F02-B Client Profile 四类摘要保持独立权威状态', () => {
  const appSource = readClientSource('src/apps/profile/ProfileApp.tsx');
  const cardSource = readClientSource('src/apps/profile/components/UserInfoCard.tsx');

  assert.match(appSource, /const \[statsState, setStatsState\]/);
  assert.match(appSource, /const \[timeState, setTimeState\]/);
  assert.match(cardSource, /const \[profileState, setProfileState\]/);
  assert.match(cardSource, /const \[balanceState, setBalanceState\]/);
  assert.match(cardSource, /getMyProfile\(t\)/);
  assert.match(cardSource, /getBalance\(t\)/);
  assert.match(cardSource, /onDirtyChange\(combinedDirty\)/);
  assert.match(cardSource, /disabled=\{profileState !== 'ready' \|\| combinedBusy\}/);
  assert.doesNotMatch(appSource, /fetch\(/);
  assert.doesNotMatch(cardSource, /fetch\(/);
});

test('R3-F02-B Client Profile stale 判定只在已有快照时成立', () => {
  assert.equal(resolveFailedSnapshotState(false), 'unavailable');
  assert.equal(resolveFailedSnapshotState(true), 'stale');
});

test('R3-F02-B WebOS Profile 同时保护浏览器与窗口关闭', () => {
  const appSource = readClientSource('src/apps/profile/ProfileApp.tsx');
  const storeSource = readClientSource('src/stores/windowStore.ts');
  const guardSource = readClientSource('src/desktop/useWindowCloseGuard.ts');

  assert.match(appSource, /useBrowserNavigationLock\(selfServiceLocked\)/);
  assert.match(appSource, /useWindowCloseGuard\(selfServiceLocked/);
  assert.match(appSource, /window\.addEventListener\('beforeunload'/);
  assert.match(storeSource, /targetWindow\?\.closeConfirmMessage/);
  assert.match(guardSource, /setWindowCloseConfirmMessage\(windowId, message\)/);
});
