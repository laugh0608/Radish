import assert from 'node:assert/strict';
import test from 'node:test';
import {
  buildProductDetailReturnTo,
  buildProductDetailSearchParams,
  normalizeProductReturnTo,
  parseProductListQuery,
  parseProductLongIdQuery,
  serializeProductListQuery,
} from '../src/pages/Products/productListUrlState.ts';

test('商品列表查询应完整回访并限制分页与筛选参数', () => {
  const query = parseProductListQuery(new URLSearchParams(
    'page=3&pageSize=50&category=badge&productType=1&isOnSale=0&keyword=radish',
  ));

  assert.deepEqual(query, {
    pageIndex: 3,
    pageSize: 50,
    categoryId: 'badge',
    productType: 1,
    isOnSale: false,
    keyword: 'radish',
  });
  assert.equal(
    serializeProductListQuery(query).toString(),
    'page=3&pageSize=50&category=badge&productType=1&isOnSale=0&keyword=radish',
  );
  assert.equal(parseProductListQuery(new URLSearchParams('page=0&pageSize=1000')).pageSize, 100);
  assert.equal(parseProductListQuery(new URLSearchParams('productType=3')).productType, undefined);
});

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

test('商品详情与订单回跳应保留列表筛选和分页上下文', () => {
  const listQuery = parseProductListQuery(new URLSearchParams(
    'page=2&pageSize=50&category=theme&isOnSale=1&keyword=night',
  ));
  const searchParams = buildProductDetailSearchParams({
    productId: '2042219067430928384',
    openDetail: true,
    listQuery,
  });

  assert.equal(searchParams.get('page'), '2');
  assert.equal(searchParams.get('pageSize'), '50');
  assert.equal(searchParams.get('category'), 'theme');
  assert.equal(searchParams.get('isOnSale'), '1');
  assert.equal(searchParams.get('keyword'), 'night');
  assert.equal(searchParams.get('productId'), '2042219067430928384');
  assert.equal(
    buildProductDetailReturnTo({ productId: '2042219067430928384', listQuery }),
    '/products?page=2&pageSize=50&category=theme&isOnSale=1&keyword=night&productId=2042219067430928384&openDetail=1',
  );
});
