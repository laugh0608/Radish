import assert from 'node:assert/strict';
import test from 'node:test';
import {
  buildProductDetailReturnTo,
  buildProductDetailSearchParams,
  normalizeProductReturnTo,
  parseProductLongIdQuery,
} from '../src/pages/Products/productListUrlState.ts';

test('商品详情 URL helper 应保留订单排障来源返回参数', () => {
  const searchParams = buildProductDetailSearchParams({
    productId: '2042219067430928384',
    openDetail: true,
    returnTo: '/orders?orderId=2042219067430928385&openDetail=1',
  });

  assert.equal(searchParams.get('productId'), '2042219067430928384');
  assert.equal(searchParams.get('openDetail'), '1');
  assert.equal(
    searchParams.get('returnTo'),
    '/orders?orderId=2042219067430928385&openDetail=1',
  );
});

test('商品详情 URL helper 应拒绝非法返回来源并保持 LongId 字符串口径', () => {
  assert.equal(parseProductLongIdQuery('2042219067430928384'), '2042219067430928384');
  assert.equal(parseProductLongIdQuery('0'), undefined);
  assert.equal(parseProductLongIdQuery('00'), undefined);
  assert.equal(parseProductLongIdQuery('02042219067430928384'), undefined);
  assert.equal(parseProductLongIdQuery('2042219067430928384.1'), undefined);
  assert.equal(normalizeProductReturnTo('/products?openDetail=1'), '/products?openDetail=1');
  assert.equal(normalizeProductReturnTo('/products?productId=2042219067430928384&openDetail=1'), '/products?productId=2042219067430928384&openDetail=1');
  assert.equal(normalizeProductReturnTo('//radishx.com/orders'), undefined);
  assert.equal(normalizeProductReturnTo('/\\radishx.com/orders'), undefined);
  assert.equal(normalizeProductReturnTo('https://radishx.com/orders'), undefined);
});

test('商品相关订单入口应回到商品详情并保留原始订单来源', () => {
  const returnTo = buildProductDetailReturnTo({
    productId: '2042219067430928384',
    returnTo: '/orders?orderId=2042219067430928385&openDetail=1',
  });

  assert.equal(
    returnTo,
    '/products?productId=2042219067430928384&openDetail=1&returnTo=%2Forders%3ForderId%3D2042219067430928385%26openDetail%3D1',
  );
});
