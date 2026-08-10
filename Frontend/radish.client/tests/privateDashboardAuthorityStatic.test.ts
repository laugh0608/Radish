import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';

function readSource(path: string): string {
  return readFileSync(new URL(path, import.meta.url), 'utf8');
}

test('通知和圈子摘要在权威读取完成前不展示伪造零值', () => {
  const notifications = readSource('../src/notifications/NotificationsApp.tsx');
  const circle = readSource('../src/circle/CircleApp.tsx');

  assert.match(notifications, /const loadState = useNotificationStore/);
  assert.match(notifications, /const hasAuthoritativeSummary = summary !== null/);
  assert.match(notifications, /notification\.web\.summaryUnavailable/);
  assert.match(circle, /CircleSummaryLoadState/);
  assert.match(circle, /hasAuthoritativeSummary \? formatCircleNumber/);
  assert.match(circle, /circle\.summaryUnavailable/);
});

test('通知偏好和宠物资料具备离开保护，宠物歧义重试复用操作键', () => {
  const notifications = readSource('../src/notifications/NotificationsApp.tsx');
  const pet = readSource('../src/pet/PetApp.tsx');

  assert.match(notifications, /useBrowserNavigationLock\(preferencesDirty\)/);
  assert.match(notifications, /navigationLocked=\{preferencesDirty\}/);
  assert.match(notifications, /beforeunload/);
  assert.match(pet, /useBrowserNavigationLock\(profileDirty\)/);
  assert.match(pet, /navigationLocked=\{profileDirty\}/);
  assert.match(pet, /beforeunload/);
  assert.match(pet, /pendingCareKeysRef/);
  assert.match(pet, /hasAuthoritativePetCareAdvance/);
  assert.match(pet, /pet\.unavailableTitle/);
});

test('Me 仪表盘拆分后保持文件边界并显式展示资料读取错误', () => {
  const app = readSource('../src/me/MeApp.tsx');
  const dashboard = readSource('../src/me/MeDashboardView.tsx');

  assert.ok(app.split('\n').length <= 1500, `MeApp.tsx 当前为 ${app.split('\n').length} 行`);
  assert.match(app, /<MeDashboardView/);
  assert.match(dashboard, /data\.errors\.profile/);
  assert.match(dashboard, /data\.errors\.assets/);
  assert.match(dashboard, /data\.errors\.browse/);
});
