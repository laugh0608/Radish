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

test('Private 仪表页保持主任务优先并统一折叠移动辅助轨', () => {
  const disclosure = readSource('../src/components/web-shell/WebTaskRailDisclosure.tsx');
  const disclosureStyles = readSource('../src/components/web-shell/WebTaskRailDisclosure.module.css');
  const notifications = readSource('../src/notifications/NotificationsApp.tsx');
  const circle = readSource('../src/circle/CircleApp.tsx');
  const circleStyles = readSource('../src/circle/CircleApp.module.css');
  const pet = readSource('../src/pet/PetApp.tsx');
  const petStyles = readSource('../src/pet/PetApp.module.css');
  const workbench = readSource('../src/workbench/WorkbenchApp.tsx');
  const dashboard = readSource('../src/me/MeDashboardView.tsx');
  const dashboardStyles = readSource('../src/me/MeApp.module.css');
  const orderDetail = readSource('../src/apps/shop/pages/OrderDetail.tsx');
  const inventory = readSource('../src/apps/shop/pages/Inventory.tsx');

  assert.match(disclosure, /aria-expanded=\{expanded\}/);
  assert.match(disclosureStyles, /@media \(max-width: 720px\)/);
  assert.match(disclosureStyles, /data-expanded='false'/);

  for (const source of [notifications, circle, pet, workbench, dashboard, orderDetail, inventory]) {
    assert.match(source, /<WebTaskRailDisclosure/);
  }

  assert.doesNotMatch(circleStyles, /\.circleRail\s*\{\s*order:\s*-1/);
  assert.doesNotMatch(petStyles, /\.petRail\s*\{\s*order:\s*-1/);
  assert.doesNotMatch(dashboardStyles, /\.subPageRail\s*\{\s*order:\s*-1/);
  assert.match(dashboard, /const recentBrowseItem = data\.browseHistory\[0\]/);
  assert.match(dashboardStyles, /grid-template-areas:[\s\S]*'revisit'[\s\S]*'details'[\s\S]*'rail'/);
  assert.doesNotMatch(inventory, /className=\{styles\.summaryGrid\}/);
});
