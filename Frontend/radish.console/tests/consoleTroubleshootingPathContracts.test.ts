import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';
import {
  buildModerationPath,
  buildModerationSearchParams,
  parseModerationLongIdQuery,
  parseModerationSectionQuery,
} from '../src/pages/Moderation/moderationPageUrlState.ts';

const testDir = dirname(fileURLToPath(import.meta.url));
const consoleRoot = resolve(testDir, '..');

function readConsoleSource(relativePath: string): string {
  return readFileSync(resolve(consoleRoot, relativePath), 'utf8');
}

test('Console 订单详情入口应同步 URL 以支持刷新恢复', () => {
  const source = readConsoleSource('src/pages/Orders/OrderList.tsx');

  assert.match(source, /buildOrderDetailSearchParams/);
  assert.match(source, /setUrlSearchParams\(\s*buildOrderDetailSearchParams\(/s);
  assert.match(source, /orderId: String\(order\.voId\)/);
  assert.match(source, /returnTo,/);
});

test('Console 跨模块订单排障入口应复用订单详情路径 helper', () => {
  const dashboardSource = readConsoleSource('src/pages/Dashboard/Dashboard.tsx');
  const userDetailSource = readConsoleSource('src/pages/Users/UserDetail.tsx');
  const coinAdminSource = readConsoleSource('src/pages/Coins/CoinAdminPage.tsx');
  const productListSource = readConsoleSource('src/pages/Products/ProductList.tsx');

  assert.match(dashboardSource, /buildOrderDetailPath/);
  assert.match(userDetailSource, /buildOrderDetailPath/);
  assert.match(coinAdminSource, /buildOrderDetailPath/);
  assert.match(productListSource, /buildOrderSearchParams/);

  assert.doesNotMatch(dashboardSource, /new URLSearchParams\(\{\s*orderNo:/s);
  assert.doesNotMatch(userDetailSource, /new URLSearchParams\(\{\s*orderId:/s);
  assert.doesNotMatch(coinAdminSource, /new URLSearchParams\(\{\s*orderId:/s);
});

test('Console 商品与订单详情 footer 动作应继承列表权限态', () => {
  const productListSource = readConsoleSource('src/pages/Products/ProductList.tsx');
  const orderListSource = readConsoleSource('src/pages/Orders/OrderList.tsx');

  assert.match(productListSource, /onEdit=\{canEditProduct \? handleEditProduct : undefined\}/);
  assert.match(orderListSource, /onRetry=\{canRetryOrder \? \(\) => \{/);
  assert.doesNotMatch(productListSource, /<ProductDetail[\s\S]*onEdit=\{handleEditProduct\}/);
  assert.doesNotMatch(orderListSource, /<OrderDetail[\s\S]*onRetry=\{\(\) => \{/);
});

test('Console 深层写入 handler 应复核权限态', () => {
  const rolePermissionSource = readConsoleSource('src/pages/Roles/RolePermissionPage.tsx');
  const documentGovernanceSource = readConsoleSource('src/pages/Documents/DocumentGovernancePage.tsx');
  const systemConfigSource = readConsoleSource('src/pages/SystemConfig/SystemConfigList.tsx');

  assert.match(rolePermissionSource, /if \(saveDisabled\) \{/);
  assert.match(documentGovernanceSource, /if \(!canUpdatePermissions \|\| accessDocument\.voIsDeleted \|\| isBuiltInDocument\(accessDocument\)\) \{/);
  assert.match(documentGovernanceSource, /if \(!canRollback \|\| !revisionDocument \|\| revisionDocument\.voIsDeleted \|\| isBuiltInDocument\(revisionDocument\)\) \{/);
  assert.match(documentGovernanceSource, /if \(!canImport\) \{/);
  assert.match(documentGovernanceSource, /if \(!canExport\) \{/);
  assert.match(systemConfigSource, /if \(!canEditSystemConfig \|\| !faviconConfig\) \{/);
  assert.match(systemConfigSource, /if \(!canEditSystemConfig \|\| !record\.voIsEditable\) \{/);
});

test('Console 内容治理入口应支持用户排障深链与来源返回', () => {
  const searchParams = buildModerationSearchParams({
    section: 'manual',
    targetUserId: '2042219067430928385',
    sourceReportId: '2042219067430928386',
    returnTo: '/users/2042219067430928385?tab=moderation',
  });

  assert.equal(searchParams.get('section'), 'manual');
  assert.equal(searchParams.get('targetUserId'), '2042219067430928385');
  assert.equal(searchParams.get('sourceReportId'), '2042219067430928386');
  assert.equal(searchParams.get('returnTo'), '/users/2042219067430928385?tab=moderation');
  assert.equal(parseModerationSectionQuery('logs'), 'logs');
  assert.equal(parseModerationSectionQuery('unknown'), undefined);
  assert.equal(parseModerationLongIdQuery('2042219067430928385'), '2042219067430928385');
  assert.equal(parseModerationLongIdQuery('02042219067430928385'), undefined);

  assert.equal(
    buildModerationPath({
      section: 'logs',
      targetUserId: '2042219067430928385',
      returnTo: 'https://radishx.com/console/users',
    }),
    '/moderation?section=logs&targetUserId=2042219067430928385',
  );
});

test('Console 用户详情应提供内容治理排障入口并复用治理 URL helper', () => {
  const userDetailSource = readConsoleSource('src/pages/Users/UserDetail.tsx');
  const moderationSource = readConsoleSource('src/pages/Moderation/ModerationPage.tsx');

  assert.match(userDetailSource, /buildModerationPath/);
  assert.match(userDetailSource, /section: 'logs'/);
  assert.match(userDetailSource, /section: 'manual'/);
  assert.match(moderationSource, /parseModerationSectionQuery/);
  assert.match(moderationSource, /parseModerationLongIdQuery/);
  assert.match(moderationSource, /返回来源/);
});
