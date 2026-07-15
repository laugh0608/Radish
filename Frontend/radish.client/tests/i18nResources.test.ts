import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';
import i18next from 'i18next';

const testDirectory = path.dirname(fileURLToPath(import.meta.url));
const localesDirectory = path.resolve(testDirectory, '../src/locales');
const domainNames = ['core', 'shell', 'discover', 'community', 'account', 'commerce', 'docs'];

function collectKeys(language: 'en' | 'zh'): string[] {
  const domainFiles = domainNames.map((domain) => path.join(localesDirectory, language, `${domain}.ts`));
  const sharedFiles = language === 'en'
    ? ['welcome.en.ts', 'welcome-open-source.en.ts']
    : ['welcome.zh.ts', 'welcome-open-source.zh.ts'];

  return [...domainFiles, ...sharedFiles.map((file) => path.join(localesDirectory, file))]
    .flatMap((file) => [...fs.readFileSync(file, 'utf8').matchAll(/^\s*'([^']+)'\s*:/gm)].map((match) => match[1]));
}

test('client 中英文资源键应完全对齐且不存在跨域重复键', () => {
  const enKeys = collectKeys('en');
  const zhKeys = collectKeys('zh');

  assert.equal(new Set(enKeys).size, enKeys.length, '英文资源存在重复键');
  assert.equal(new Set(zhKeys).size, zhKeys.length, '中文资源存在重复键');
  assert.deepEqual([...new Set(enKeys)].sort(), [...new Set(zhKeys)].sort());
});

test('client 单个业务域资源文件不得超过 900 行', () => {
  for (const language of ['en', 'zh']) {
    for (const domain of domainNames) {
      const file = path.join(localesDirectory, language, `${domain}.ts`);
      const lineCount = fs.readFileSync(file, 'utf8').split('\n').length;
      assert.ok(lineCount <= 900, `${language}/${domain}.ts 已膨胀到 ${lineCount} 行`);
    }
  }
});

test('client 高频数量文案应按英文单复数规则解析', async () => {
  const enKeys = collectKeys('en');
  assert.ok(enKeys.includes('shop.productCount_one'));
  assert.ok(enKeys.includes('shop.productCount_other'));
  assert.ok(enKeys.includes('forum.quickReply.total_one'));
  assert.ok(enKeys.includes('forum.quickReply.total_other'));
  assert.ok(enKeys.includes('me.assets.transactionCount_one'));
  assert.ok(enKeys.includes('me.assets.transactionCount_other'));
  assert.ok(enKeys.includes('circle.resultCount_one'));
  assert.ok(enKeys.includes('circle.resultCount_other'));
  assert.ok(!enKeys.includes('shop.productCount'));

  const instance = i18next.createInstance();
  await instance.init({
    lng: 'en',
    resources: {
      en: {
        translation: {
          'shop.productCount_one': '{{count}} product',
          'shop.productCount_other': '{{count}} products',
          'forum.quickReply.total_one': '{{count}} reply',
          'forum.quickReply.total_other': '{{count}} replies',
          'me.assets.transactionCount_one': '{{count}} record',
          'me.assets.transactionCount_other': '{{count}} records',
          'circle.resultCount_one': '{{count}} item',
          'circle.resultCount_other': '{{count}} items',
        },
      },
      zh: {
        translation: {
          'shop.productCount_one': '{{count}} 件商品',
          'shop.productCount_other': '{{count}} 件商品',
        },
      },
    },
  });

  assert.equal(instance.t('shop.productCount', { count: 1 }), '1 product');
  assert.equal(instance.t('shop.productCount', { count: 2 }), '2 products');
  assert.equal(instance.t('forum.quickReply.total', { count: 1 }), '1 reply');
  assert.equal(instance.t('forum.quickReply.total', { count: 2 }), '2 replies');
  assert.equal(instance.t('me.assets.transactionCount', { count: 0 }), '0 records');
  assert.equal(instance.t('me.assets.transactionCount', { count: 1 }), '1 record');
  assert.equal(instance.t('me.assets.transactionCount', { count: 2 }), '2 records');
  assert.equal(instance.t('circle.resultCount', { count: 1 }), '1 item');
  assert.equal(instance.t('circle.resultCount', { count: 2 }), '2 items');

  await instance.changeLanguage('zh');
  assert.equal(instance.t('shop.productCount', { count: 2 }), '2 件商品');
});
