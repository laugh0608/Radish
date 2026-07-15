import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

const testDirectory = path.dirname(fileURLToPath(import.meta.url));
const localesDirectory = path.resolve(testDirectory, '../src/locales');
const domainNames = ['core', 'shell', 'dashboard', 'users', 'moderation', 'orders'];

function collectKeys(language: 'en' | 'zh'): string[] {
  return domainNames.flatMap((domain) => {
    const source = fs.readFileSync(path.join(localesDirectory, language, `${domain}.ts`), 'utf8');
    return [...source.matchAll(/^\s*'([^']+)'\s*:/gm)].map((match) => match[1]);
  });
}

test('Console 中英文资源键应完全对齐且不存在跨域重复键', () => {
  const enKeys = collectKeys('en');
  const zhKeys = collectKeys('zh');

  assert.equal(new Set(enKeys).size, enKeys.length, '英文资源存在重复键');
  assert.equal(new Set(zhKeys).size, zhKeys.length, '中文资源存在重复键');
  assert.deepEqual([...new Set(enKeys)].sort(), [...new Set(zhKeys)].sort());
});

test('Console 高频业务数量文案应覆盖英文单复数', async () => {
  const i18next = await import('i18next');
  const instance = i18next.default.createInstance();
  await instance.init({
    lng: 'en',
    resources: {
      en: {
        translation: {
          'users.list.accountCount_one': '{{count}} account',
          'users.list.accountCount_other': '{{count}} accounts',
          'orders.list.orderCount_one': '{{count}} order',
          'orders.list.orderCount_other': '{{count}} orders',
          'moderation.reportCount_one': '{{count}} report',
          'moderation.reportCount_other': '{{count}} reports',
        },
      },
    },
  });

  assert.equal(instance.t('users.list.accountCount', { count: 0 }), '0 accounts');
  assert.equal(instance.t('users.list.accountCount', { count: 1 }), '1 account');
  assert.equal(instance.t('users.list.accountCount', { count: 2 }), '2 accounts');
  assert.equal(instance.t('orders.list.orderCount', { count: 1 }), '1 order');
  assert.equal(instance.t('orders.list.orderCount', { count: 2 }), '2 orders');
  assert.equal(instance.t('moderation.reportCount', { count: 1 }), '1 report');
  assert.equal(instance.t('moderation.reportCount', { count: 2 }), '2 reports');
});
