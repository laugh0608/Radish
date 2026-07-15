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
  assert.ok(enKeys.includes('pet.care.remaining_one'));
  assert.ok(enKeys.includes('pet.care.remaining_other'));
  assert.ok(enKeys.includes('pet.care.cooldownRemaining_minute_one'));
  assert.ok(enKeys.includes('pet.care.cooldownRemaining_minute_other'));
  assert.ok(enKeys.includes('experience.bar.remainingExperience_one'));
  assert.ok(enKeys.includes('experience.bar.remainingExperience_other'));
  assert.ok(enKeys.includes('experience.chart.rangeDays_one'));
  assert.ok(enKeys.includes('experience.chart.rangeDays_other'));
  assert.ok(enKeys.includes('pit.overview.recentCount_one'));
  assert.ok(enKeys.includes('pit.overview.recentCount_other'));
  assert.ok(enKeys.includes('pit.statistics.transactionCount_one'));
  assert.ok(enKeys.includes('pit.statistics.transactionCount_other'));
  assert.ok(enKeys.includes('profile.transactions.pageInfo_one'));
  assert.ok(enKeys.includes('profile.transactions.pageInfo_other'));
  assert.ok(enKeys.includes('pit.security.account.unlockRemaining_one'));
  assert.ok(enKeys.includes('pit.security.account.unlockRemaining_other'));
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
          'pet.care.remaining_one': '{{count}} use left today',
          'pet.care.remaining_other': '{{count}} uses left today',
          'pet.care.cooldownRemaining_minute_one': 'About {{count}} minute left',
          'pet.care.cooldownRemaining_minute_other': 'About {{count}} minutes left',
          'experience.bar.remainingExperience_one': '{{value}} EXP point needed',
          'experience.bar.remainingExperience_other': '{{value}} EXP points needed',
          'experience.chart.rangeDays_one': 'Last {{count}} day',
          'experience.chart.rangeDays_other': 'Last {{count}} days',
          'pit.overview.recentCount_one': '{{value}} recent transaction',
          'pit.overview.recentCount_other': '{{value}} recent transactions',
          'pit.statistics.transactionCount_one': '{{value}} transaction',
          'pit.statistics.transactionCount_other': '{{value}} transactions',
          'profile.transactions.pageInfo_one': 'Page {{current}} / {{total}} ({{value}} transaction)',
          'profile.transactions.pageInfo_other': 'Page {{current}} / {{total}} ({{value}} transactions)',
          'pit.security.account.unlockRemaining_one': 'Unlocks in about {{value}} minute',
          'pit.security.account.unlockRemaining_other': 'Unlocks in about {{value}} minutes',
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
  assert.equal(instance.t('pet.care.remaining', { count: 1 }), '1 use left today');
  assert.equal(instance.t('pet.care.remaining', { count: 2 }), '2 uses left today');
  assert.equal(instance.t('pet.care.cooldownRemaining', { context: 'minute', count: 1 }), 'About 1 minute left');
  assert.equal(instance.t('pet.care.cooldownRemaining', { context: 'minute', count: 2 }), 'About 2 minutes left');
  assert.equal(instance.t('experience.bar.remainingExperience', { count: 0, value: '0' }), '0 EXP points needed');
  assert.equal(instance.t('experience.bar.remainingExperience', { count: 1, value: '1' }), '1 EXP point needed');
  assert.equal(instance.t('experience.bar.remainingExperience', { count: 2, value: '2' }), '2 EXP points needed');
  assert.equal(instance.t('experience.chart.rangeDays', { count: 1 }), 'Last 1 day');
  assert.equal(instance.t('experience.chart.rangeDays', { count: 2 }), 'Last 2 days');
  assert.equal(instance.t('pit.overview.recentCount', { count: 1, value: '1' }), '1 recent transaction');
  assert.equal(instance.t('pit.overview.recentCount', { count: 2, value: '2' }), '2 recent transactions');
  assert.equal(instance.t('pit.statistics.transactionCount', { count: 1, value: '1' }), '1 transaction');
  assert.equal(instance.t('pit.statistics.transactionCount', { count: 2, value: '2' }), '2 transactions');
  assert.equal(instance.t('profile.transactions.pageInfo', { current: '1', total: '2', count: 1, value: '1' }), 'Page 1 / 2 (1 transaction)');
  assert.equal(instance.t('profile.transactions.pageInfo', { current: '1', total: '2', count: 2, value: '2' }), 'Page 1 / 2 (2 transactions)');
  assert.equal(instance.t('pit.security.account.unlockRemaining', { count: 1, value: '1' }), 'Unlocks in about 1 minute');
  assert.equal(instance.t('pit.security.account.unlockRemaining', { count: 2, value: '2' }), 'Unlocks in about 2 minutes');

  await instance.changeLanguage('zh');
  assert.equal(instance.t('shop.productCount', { count: 2 }), '2 件商品');
});
