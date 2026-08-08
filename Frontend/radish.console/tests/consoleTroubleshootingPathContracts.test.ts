import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';
import {
  buildModerationPath,
  buildModerationSearchParams,
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
  assert.match(orderListSource, /onRetry=\{canRetryOrder \? handleRetry : undefined\}/);
  assert.doesNotMatch(productListSource, /<ProductDetail[\s\S]*onEdit=\{handleEditProduct\}/);
  assert.doesNotMatch(orderListSource, /<OrderDetail[\s\S]*onRetry=\{handleRetry\}/);
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
    keyword: '2042219067430928385',
    returnTo: '/users/2042219067430928385?tab=moderation',
  });

  assert.equal(searchParams.get('keyword'), '2042219067430928385');
  assert.equal(searchParams.get('returnTo'), '/users/2042219067430928385?tab=moderation');

  assert.equal(
    buildModerationPath({
      keyword: '2042219067430928385',
      returnTo: 'https://radishx.com/console/users',
    }),
    '/moderation?keyword=2042219067430928385',
  );

  assert.equal(
    buildModerationPath({
      view: 'appeals',
      appealPublicId: 'appeal_public',
      returnTo: '/users/2042219067430928385?tab=moderation',
    }),
    '/moderation?view=appeals&appeal=appeal_public&returnTo=%2Fusers%2F2042219067430928385%3Ftab%3Dmoderation',
  );
});

test('Console 用户详情应提供内容治理排障入口并复用治理 URL helper', () => {
  const userDetailSource = readConsoleSource('src/pages/Users/UserDetail.tsx');
  const moderationSource = readConsoleSource('src/pages/Moderation/ModerationPage.tsx');

  assert.match(userDetailSource, /buildModerationPath/);
  assert.match(userDetailSource, /keyword: userId/);
  assert.doesNotMatch(userDetailSource, /section: 'manual'/);
  assert.doesNotMatch(userDetailSource, /section: 'logs'/);
  assert.match(moderationSource, /searchParams\.get\('keyword'\)/);
  assert.match(moderationSource, /t\('moderation\.backToSource'\)/);
});

test('Console 移动边界应固定高频导航与低风险治理顺序', () => {
  const layoutSource = readConsoleSource('src/components/AdminLayout/AdminLayout.tsx');
  const moderationSource = readConsoleSource('src/pages/Moderation/ModerationPage.tsx');
  const moderationStyles = readConsoleSource('src/pages/Moderation/index.css');

  assert.match(layoutSource, /label: t\('console\.mobile\.overview'\)/);
  assert.match(layoutSource, /label: t\('console\.mobile\.governance'\)/);
  assert.match(layoutSource, /label: t\('console\.mobile\.commerce'\)/);
  assert.match(layoutSource, /label: t\('console\.mobile\.access'\)/);
  assert.match(layoutSource, /label: t\('console\.mobile\.more'\)/);
  assert.match(layoutSource, /aria-label=\{t\('console\.mobile\.navLabel'\)\}/);
  assert.match(layoutSource, /t\('console\.mobile\.allDescription'\)/);
  assert.match(moderationSource, /canAction \? 'warning' : 'neutral'/);
  assert.match(moderationStyles, /@media \(max-width: 1100px\)[\s\S]*grid-template-columns: 1fr/);
  assert.match(moderationStyles, /@media \(max-width: 768px\)[\s\S]*moderation-case-form-grid/);
});

test('Console 应提供安全的 client 来源返回入口与 Web 主线对象回看', () => {
  const mainSource = readConsoleSource('src/main.tsx');
  const layoutSource = readConsoleSource('src/components/AdminLayout/AdminLayout.tsx');
  const clientBackLinkSource = readConsoleSource('src/components/ClientBackLink/ClientBackLink.tsx');
  const loginSource = readConsoleSource('src/pages/Login/Login.tsx');
  const oidcCallbackSource = readConsoleSource('src/pages/OidcCallback/OidcCallback.tsx');
  const routerComponentsSource = readConsoleSource('src/router/routerComponents.tsx');
  const moderationSource = readConsoleSource('src/pages/Moderation/ModerationPage.tsx');

  assert.match(mainSource, /rememberClientBackTo\(window\.location\.search\)/);
  assert.match(layoutSource, /<ClientBackLink \/>/);
  assert.match(clientBackLinkSource, /href=\{clientBackTo\}/);
  assert.match(clientBackLinkSource, /clearRememberedClientBackTo\(\)/);
  assert.match(clientBackLinkSource, /t\('console\.clientBack\.returning'\)/);
  assert.match(loginSource, /<ClientBackLink \/>/);
  assert.match(oidcCallbackSource, /error \? <ClientBackLink \/>/);
  assert.match(routerComponentsSource, /<ClientBackLink \/>/);
  assert.match(moderationSource, /normalizeConsoleReturnTo/);
  assert.doesNotMatch(moderationSource, /new URL\('\/desktop'/);
});

test('Console 内容治理应只消费案件 API 并保留冲突草稿', () => {
  const moderationSource = readConsoleSource('src/pages/Moderation/ModerationPage.tsx');
  const appealSource = readConsoleSource('src/pages/Moderation/ModerationAppealsWorkspace.tsx');
  const appealStyles = readConsoleSource('src/pages/Moderation/ModerationAppealsWorkspace.css');
  const apiSource = readConsoleSource('src/api/moderationApi.ts');

  assert.match(apiSource, /ContentModeration\/GetCaseQueue/);
  assert.match(apiSource, /ContentModeration\/ReviewCase/);
  assert.match(apiSource, /ContentModeration\/ApplyCorrectiveAction/);
  assert.doesNotMatch(apiSource, /ContentModeration\/GetReviewQueue/);
  assert.doesNotMatch(apiSource, /ContentModeration\/Review['"`]/);
  assert.doesNotMatch(apiSource, /ContentModeration\/ApplyUserAction/);
  assert.doesNotMatch(apiSource, /ContentModeration\/GetActionLogs/);
  assert.match(moderationSource, /loadCase\(casePublicId, false\)/);
  assert.match(moderationSource, /conflictDraftPreserved/);
  assert.match(apiSource, /ContentModeration\/GetAppealQueue/);
  assert.match(apiSource, /ContentModeration\/ReviewAppeal/);
  assert.match(apiSource, /ContentModeration\/ExecuteAppealRelief/);
  assert.match(apiSource, /ContentModerationAppealActionResultVo/);
  assert.match(appealSource, /CONSOLE_PERMISSIONS\.moderationAppeal/);
  assert.match(appealSource, /CONSOLE_PERMISSIONS\.moderationAction/);
  assert.match(appealSource, /selectedSummary[\s\S]*canAction[\s\S]*executeModerationAppealRelief/);
  assert.match(appealSource, /actionOnlyDescription/);
  assert.match(appealSource, /caseDetail\.voCase\.voDecision/);
  assert.match(appealSource, /caseDetail\.voCase\.voTargetDisposition/);
  assert.match(appealSource, /caseDetail\?\.voPublicResultCode/);
  assert.match(appealSource, /loadAppeal\(selectedAppealId, false\)/);
  assert.match(appealSource, /conflictDraftPreserved/);
  assert.match(appealSource, /linkedAppealId && canAppeal/);
  assert.match(appealSource, /appealPublicId,[\s\S]*replace: true/);
  assert.match(appealStyles, /@media \(max-width: 768px\)[\s\S]*moderation-appeal-write-panel[\s\S]*display: none/);
  assert.match(appealStyles, /@media \(max-width: 768px\)[\s\S]*moderation-appeal-desktop-action[\s\S]*display: none/);
});
