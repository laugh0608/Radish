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

test('Console 订单详情入口应同步 URL 以支持刷新恢复', () => {
  const source = readConsoleSource('src/pages/Orders/OrderList.tsx');

  assert.match(source, /buildOrderDetailSearchParams/);
  assert.match(source, /setUrlSearchParams\(\s*buildOrderDetailSearchParams\(/s);
  assert.match(source, /orderId: String\(order\.voId\)/);
  assert.match(source, /returnTo,/);
});

test('Console 跨模块订单排障入口应复用订单详情路径 helper', () => {
  const userDetailSource = readConsoleSource('src/pages/Users/UserDetail.tsx');
  const coinAdminSource = readConsoleSource('src/pages/Coins/CoinAdminPage.tsx');
  const productListSource = readConsoleSource('src/pages/Products/ProductList.tsx');

  assert.match(userDetailSource, /buildOrderDetailPath/);
  assert.match(coinAdminSource, /buildOrderDetailPath/);
  assert.match(productListSource, /buildOrderSearchParams/);

  assert.doesNotMatch(userDetailSource, /new URLSearchParams\(\{\s*orderId:/s);
  assert.doesNotMatch(coinAdminSource, /new URLSearchParams\(\{\s*orderId:/s);
});
