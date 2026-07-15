import assert from 'node:assert/strict';
import test from 'node:test';
import {
  getOrderDurationLabel,
  isOrderInStatus,
  normalizeOrderStatus,
} from '../src/pages/Orders/orderPresentation.ts';

test('订单状态控制流只接受稳定枚举值或名称，不依赖展示文案', () => {
  assert.equal(normalizeOrderStatus(2), 'Completed');
  assert.equal(normalizeOrderStatus('Completed'), 'Completed');
  assert.equal(normalizeOrderStatus('已完成'), 'Unknown');
  assert.equal(isOrderInStatus({ voStatus: 'Failed' } as never, 'Failed'), true);
  assert.equal(isOrderInStatus({ voStatus: '履约失败' } as never, 'Failed'), false);
});

test('订单有效期展示使用结构化快照字段', () => {
  const t = (key: string, options?: Record<string, unknown>) => (
    options?.count === undefined ? key : `${key}:${options.count}`
  );

  assert.equal(getOrderDurationLabel({
    voDurationType: 'Days',
    voDurationDays: 2,
    voFixedExpiresAt: null,
  }, t as never, String), 'orders.duration.days:2');
  assert.equal(getOrderDurationLabel({
    voDurationType: 'FixedDate',
    voDurationDays: null,
    voFixedExpiresAt: '2026-08-01T00:00:00Z',
  }, t as never, (value) => value.slice(0, 10)), 'orders.duration.until');
});
