import assert from 'node:assert/strict';
import test from 'node:test';
import {
  formatLocalizedDateTime,
  formatLocalizedNumber,
  formatLocalizedRelativeTime,
  resolveIntlLocale,
} from '../src/utils/intl.ts';

test('resolveIntlLocale 应把 Radish 支持语言收敛到 Intl locale', () => {
  assert.equal(resolveIntlLocale('en'), 'en-US');
  assert.equal(resolveIntlLocale('en-GB'), 'en-US');
  assert.equal(resolveIntlLocale('zh-CN'), 'zh-CN');
  assert.equal(resolveIntlLocale(undefined), 'zh-CN');
});

test('共享格式化应按语言处理数字、日期和相对时间', () => {
  const instant = new Date('2026-07-14T08:30:00.000Z');

  assert.equal(formatLocalizedNumber(1234.5, 'en'), '1,234.5');
  assert.equal(formatLocalizedNumber(1234.5, 'zh'), '1,234.5');
  assert.equal(
    formatLocalizedDateTime(instant, 'en', {
      timeZone: 'UTC',
      year: 'numeric',
      month: 'short',
      day: '2-digit',
    }),
    'Jul 14, 2026'
  );
  assert.equal(
    formatLocalizedRelativeTime(instant, 'en', new Date('2026-07-14T09:30:00.000Z').getTime()),
    '1 hour ago'
  );
  assert.equal(
    formatLocalizedRelativeTime(instant, 'zh', new Date('2026-07-14T09:30:00.000Z').getTime()),
    '1小时前'
  );
});

test('非法日期应保留原始输入，避免伪造有效时间', () => {
  assert.equal(formatLocalizedDateTime('not-a-date', 'en'), 'not-a-date');
  assert.equal(formatLocalizedRelativeTime('not-a-date', 'zh'), 'not-a-date');
});
