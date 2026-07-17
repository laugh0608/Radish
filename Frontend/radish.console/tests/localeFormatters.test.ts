import assert from 'node:assert/strict';
import test from 'node:test';
import {
  formatConsoleDate,
  formatConsoleInteger,
  formatConsoleMonthDay,
  formatConsoleSignedInteger,
  formatConsoleWeekday,
} from '../src/utils/localeFormatters.ts';

test('Console 自然日期格式化不应受 UTC 日期解析偏移影响', () => {
  assert.equal(formatConsoleDate('2026-01-02', 'en'), '01/02/2026');
  assert.equal(formatConsoleMonthDay('2026-01-02', 'en'), '01/02');
  assert.equal(formatConsoleWeekday('2026-01-02', 'en'), 'Fri');
});

test('Console long 整数格式化应保持字符串精度与符号', () => {
  assert.equal(formatConsoleInteger('9223372036854775807', 'en'), '9,223,372,036,854,775,807');
  assert.equal(formatConsoleSignedInteger('9223372036854775807', 'en'), '+9,223,372,036,854,775,807');
  assert.equal(formatConsoleSignedInteger('-9223372036854775807', 'en'), '-9,223,372,036,854,775,807');
});
