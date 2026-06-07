import assert from 'node:assert/strict';
import test from 'node:test';
import {
  buildOrderSearchParams,
  normalizeConsoleReturnTo,
  parseLongIdQuery,
} from '../src/pages/Orders/orderListUrlState.ts';

test('buildOrderSearchParams 应保留订单排障来源返回参数', () => {
  const searchParams = buildOrderSearchParams({
    orderId: '2042219067430928384',
    userId: '2042219067430928385',
    productId: '2042219067430928386',
    openDetail: true,
    returnTo: '/coins?userId=2042219067430928385&businessId=2042219067430928384',
  });

  assert.equal(searchParams.get('orderId'), '2042219067430928384');
  assert.equal(searchParams.get('userId'), '2042219067430928385');
  assert.equal(searchParams.get('productId'), '2042219067430928386');
  assert.equal(searchParams.get('openDetail'), '1');
  assert.equal(
    searchParams.get('returnTo'),
    '/coins?userId=2042219067430928385&businessId=2042219067430928384',
  );
});

test('buildOrderSearchParams 关闭详情后仍保留来源返回参数', () => {
  const searchParams = buildOrderSearchParams({
    userId: '2042219067430928385',
    pageIndex: 2,
    returnTo: '/coins?userId=2042219067430928385',
  });

  assert.equal(searchParams.get('openDetail'), null);
  assert.equal(searchParams.get('pageIndex'), '2');
  assert.equal(searchParams.get('returnTo'), '/coins?userId=2042219067430928385');
});

test('订单 URL helper 应保留 LongId 字符串并拒绝非法返回来源', () => {
  assert.equal(parseLongIdQuery('2042219067430928384'), '2042219067430928384');
  assert.equal(parseLongIdQuery('0'), undefined);
  assert.equal(parseLongIdQuery('02042219067430928384'), undefined);
  assert.equal(parseLongIdQuery('2042219067430928384.1'), undefined);
  assert.equal(normalizeConsoleReturnTo('/orders?openDetail=1'), '/orders?openDetail=1');
  assert.equal(normalizeConsoleReturnTo('//radishx.com/coins'), undefined);
  assert.equal(normalizeConsoleReturnTo('/\\radishx.com/coins'), undefined);
  assert.equal(normalizeConsoleReturnTo('https://radishx.com/coins'), undefined);
});
