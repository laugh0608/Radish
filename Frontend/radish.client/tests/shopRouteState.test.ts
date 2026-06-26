import assert from 'node:assert/strict';
import test from 'node:test';
import {
  buildShopPath,
  createDefaultShopRoute,
  isShopPathname,
  parseShopRoute,
} from '../src/shop/shopRouteState.ts';

test('parseShopRoute 应解析正式 Web 商城私域入口', () => {
  assert.deepEqual(parseShopRoute('/shop/orders'), {
    kind: 'orders',
  });
  assert.deepEqual(parseShopRoute('/shop/orders/'), {
    kind: 'orders',
  });
  assert.deepEqual(parseShopRoute('/shop/order/2042219067430928385'), {
    kind: 'order-detail',
    orderId: '2042219067430928385',
  });
  assert.deepEqual(parseShopRoute('/shop/order/2042219067430928385/'), {
    kind: 'order-detail',
    orderId: '2042219067430928385',
  });
  assert.deepEqual(parseShopRoute('/shop/inventory'), {
    kind: 'inventory',
  });
});

test('parseShopRoute 应拒绝公开商城和非法私域 ID', () => {
  assert.equal(parseShopRoute('/shop'), null);
  assert.equal(parseShopRoute('/shop/products'), null);
  assert.equal(parseShopRoute('/shop/product/2042219067430928384'), null);
  assert.equal(parseShopRoute('/shop/order/0'), null);
  assert.equal(parseShopRoute('/shop/order/abc'), null);
});

test('buildShopPath 应稳定回写正式 Web 商城私域路径', () => {
  assert.equal(buildShopPath(createDefaultShopRoute()), '/shop/orders');
  assert.equal(buildShopPath({
    kind: 'order-detail',
    orderId: '2042219067430928385',
  }), '/shop/order/2042219067430928385');
  assert.equal(buildShopPath({
    kind: 'inventory',
  }), '/shop/inventory');
});

test('isShopPathname 应只识别正式 Web 商城私域入口', () => {
  assert.equal(isShopPathname('/shop/orders'), true);
  assert.equal(isShopPathname('/shop/order/2042219067430928385'), true);
  assert.equal(isShopPathname('/shop/inventory'), true);
  assert.equal(isShopPathname('/shop'), false);
  assert.equal(isShopPathname('/shop/product/2042219067430928384'), false);
});
