import assert from 'node:assert/strict';
import test from 'node:test';
import { formatCircleDateTime, formatCircleNumber } from '../src/circle/circlePresentation.ts';

test('圈子数字展示应跟随当前语言区域', () => {
  assert.equal(formatCircleNumber(12345, 'zh'), '12,345');
  assert.equal(formatCircleNumber(12345, 'en'), '12,345');
});

test('圈子日期展示应跟随当前语言区域并保留时区', () => {
  const value = '2026-07-15T04:30:00Z';

  assert.equal(formatCircleDateTime(value, 'Asia/Shanghai', 'zh'), '2026-07-15 12:30:00');
  assert.match(formatCircleDateTime(value, 'Asia/Shanghai', 'en'), /07\/15\/2026, 12:30:00/);
  assert.equal(formatCircleDateTime('invalid', 'Asia/Shanghai', 'en', 'Unknown'), 'Unknown');
});
