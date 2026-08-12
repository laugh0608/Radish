import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';

const readSource = (relativePath: string) => readFileSync(
  new URL(`../${relativePath}`, import.meta.url),
  'utf8',
);

test('R3-C05-A Dashboard 统计与最近订单应维护独立权威快照和请求代际', () => {
  const page = readSource('src/pages/Dashboard/Dashboard.tsx');

  assert.match(page, /type DashboardReadState = 'loading' \| 'ready' \| 'unavailable' \| 'stale'/);
  assert.match(page, /useState<DashboardStatsVo \| null>\(null\)/);
  assert.match(page, /useState<Order\[\] \| null>\(null\)/);
  assert.match(page, /requestGeneration !== statsRequestGeneration\.current/);
  assert.match(page, /requestGeneration !== ordersRequestGeneration\.current/);
  assert.match(page, /setStatsState\(hasCurrentSnapshot \? 'stale' : 'unavailable'\)/);
  assert.match(page, /setOrdersState\(hasCurrentSnapshot \? 'stale' : 'unavailable'\)/);
  assert.doesNotMatch(page, /useState<DashboardStatsVo>\(\{/);
});

test('R3-C05-A Dashboard 应提供单一刷新入口并准确标注静态任务路径', () => {
  const page = readSource('src/pages/Dashboard/Dashboard.tsx');
  const zhLocale = readSource('src/locales/zh/dashboard.ts');

  assert.match(page, /const handleRefresh = \(\) => \{[\s\S]*?loadStats\(\)[\s\S]*?loadRecentOrders\(\)/);
  assert.match(page, /dashboard\.taskPaths\.title/);
  assert.match(zhLocale, /'dashboard\.taskPaths\.description': '.*不是实时待办队列。'/);
  assert.doesNotMatch(page, /dashboard\.commands|dashboard\.command|createProduct/);
  assert.doesNotMatch(page, /getOrderTrend|getProductSalesRanking|getUserLevelDistribution/);
});

test('R3-C05-A 最近订单应由同一快照承载 PC 表格与 Mobile 卡片', () => {
  const page = readSource('src/pages/Dashboard/Dashboard.tsx');
  const styles = readSource('src/pages/Dashboard/Dashboard.css');

  assert.match(page, /recentOrders !== null \|\| ordersState === 'loading'[\s\S]*?dashboard-orders-desktop[\s\S]*?dataSource=\{recentOrders \?\? \[\]\}/);
  assert.match(page, /dashboard-orders-mobile[\s\S]*?recentOrders\.map/);
  assert.match(page, /console-resource-mobile-card dashboard-order-mobile-card/);
  assert.match(styles, /@media \(max-width: 768px\)[\s\S]*?\.dashboard-orders-desktop \{[\s\S]*?display: none/);
  assert.match(styles, /@media \(max-width: 768px\)[\s\S]*?\.dashboard-orders-mobile \{[\s\S]*?display: grid/);
});

test('R3-C05-A Dashboard 读取错误应保留结构化响应信息并本地化回退', () => {
  const page = readSource('src/pages/Dashboard/Dashboard.tsx');
  const statisticsApi = readSource('src/api/statisticsApi.ts');

  assert.match(statisticsApi, /createApiResponseError/);
  assert.doesNotMatch(statisticsApi, /throw new Error/);
  assert.match(page, /getDashboardStats\(t\)/);
  assert.match(page, /adminGetOrders\(\{[\s\S]*?pageSize: 5,[\s\S]*?\}, t\)/);
  assert.match(page, /getLocalizedApiErrorMessage\(error, t, 'dashboard\.loadStatsFailed'\)/);
  assert.match(page, /getLocalizedApiErrorMessage\(error, t, 'dashboard\.loadOrdersFailed'\)/);
});
