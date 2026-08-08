import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

const testDir = dirname(fileURLToPath(import.meta.url));
const consoleRoot = resolve(testDir, '..');

function readConsoleSource(relativePath: string): string {
  return readFileSync(resolve(consoleRoot, relativePath), 'utf8');
}

test('R1-C01 列表应固定 PC 薄表格、Mobile 三列行与按需筛选层', () => {
  const source = readConsoleSource('src/pages/Orders/OrderList.tsx');
  const styles = readConsoleSource('src/pages/Orders/OrderList.css');

  assert.match(source, /<BottomSheet/);
  assert.match(source, /className="order-mobile-row"/);
  assert.match(source, /orders\.list\.paymentEvidence/);
  assert.match(source, /orders\.status\.failed/);
  assert.match(source, /inputMode="numeric"/);
  assert.doesNotMatch(source, /type="number"/);
  assert.match(styles, /grid-template-columns: minmax\(0, 1\.08fr\) minmax\(0, 1\.12fr\) minmax\(70px, 0\.72fr\)/);
  assert.match(styles, /\.order-desktop-table \.ant-table-tbody > tr > td[\s\S]*height: 48px/);
});

test('R1-C01 详情应按权威读取失败关闭写操作并保持既有资源回跳', () => {
  const source = readConsoleSource('src/pages/Orders/OrderDetail.tsx');
  const snapshotIndex = source.indexOf("orders.detail.group.snapshot");
  const paymentIndex = source.indexOf("orders.detail.group.payment");
  const fulfillmentIndex = source.indexOf("orders.detail.group.fulfillment");
  const remarkIndex = source.indexOf("orders.detail.adminRemark");

  assert.match(source, /data-console-fullscreen-task="orders"/);
  assert.match(source, /const actionsAreAuthoritative = readState === 'ready'/);
  assert.match(source, /isApiResponseNotFoundError/);
  assert.match(source, /currentOrder\.voCanRetryFulfillment === true && onRetry/);
  assert.match(source, /canRemark && onSaveRemark/);
  assert.match(source, /onViewUser/);
  assert.match(source, /onViewProduct/);
  assert.match(source, /onViewCoinTransaction/);
  assert.ok(snapshotIndex >= 0 && snapshotIndex < paymentIndex);
  assert.ok(paymentIndex < fulfillmentIndex);
  assert.ok(fulfillmentIndex < remarkIndex);
  assert.doesNotMatch(source, /<Modal/);
});

test('Console Mobile 全屏订单任务应隐藏全局导航且五项入口沿用胶囊视觉', () => {
  const source = readConsoleSource('src/components/AdminLayout/AdminLayout.tsx');
  const styles = readConsoleSource('src/components/AdminLayout/AdminLayout.css');

  assert.match(source, /const mobileTaskActive = isMobileLayout && isOrderDetailTask/);
  assert.match(source, /isMobileLayout && !mobileTaskActive/);
  assert.match(source, /!mobileTaskActive \? <AppBreadcrumb \/>/);
  assert.match(styles, /\.admin-content--mobile-task[\s\S]*height: 100dvh/);
  assert.match(styles, /\.admin-mobile-nav[\s\S]*border-radius: 999px/);
  assert.match(styles, /\.admin-mobile-nav__item[\s\S]*border-radius: 999px/);
});

test('订单重试继续使用既有 API，但产品文案覆盖所有履约类型', () => {
  const apiSource = readConsoleSource('src/api/shopApi.ts');
  const zhSource = readConsoleSource('src/locales/zh/orders.ts');
  const enSource = readConsoleSource('src/locales/en/orders.ts');

  assert.match(apiSource, /Shop\/RetryGrantBenefit/);
  assert.match(zhSource, /'orders\.action\.retryFulfillment': '重试发放'/);
  assert.match(zhSource, /'orders\.detail\.retry': '重试发放'/);
  assert.match(enSource, /'orders\.detail\.retry': 'Retry fulfillment'/);
  assert.doesNotMatch(zhSource, /重新发放订单.*权益/);
  assert.doesNotMatch(enSource, /Retry entitlement fulfillment for order/);
});
