import assert from 'node:assert/strict';
import test from 'node:test';
import i18next from 'i18next';
import {
  absoluteCoinValue,
  addCoinValues,
  compareCoinValues,
  formatCoinAmount,
  formatCoinChartDate,
  formatCoinDateTime,
  formatCoinNumber,
  formatStatisticsCategory,
  formatTransactionStatus,
  formatTransactionType,
  getSignedTransactionAmount,
  resolveTransactionDirection,
  subtractCoinValues,
} from '../src/coin/coinPresentation.ts';
import type { CoinTransaction } from '../src/api/coin.ts';

function createTransaction(overrides: Partial<CoinTransaction> = {}): CoinTransaction {
  return {
    voId: '70001',
    voTransactionNo: 'TXN_70001',
    voFromUserId: '10001',
    voFromUserName: 'Sender',
    voToUserId: '20002',
    voToUserName: 'Recipient',
    voAmount: '9007199254740993',
    voAmountDisplay: '不应消费的金额展示字段',
    voFee: '0',
    voFeeDisplay: '不应消费的手续费展示字段',
    voTransactionType: 'TRANSFER',
    voTransactionTypeDisplay: '不应消费的交易类型展示字段',
    voStatus: 'SUCCESS',
    voStatusDisplay: '不应消费的状态展示字段',
    voBusinessType: null,
    voBusinessId: null,
    voRemark: null,
    voCreateTime: '2026-07-15T04:30:00.000Z',
    ...overrides,
  };
}

test('萝卜金额应按 long 字符串安全运算并使用 locale 格式化', async () => {
  const instance = i18next.createInstance();
  await instance.init({
    lng: 'en',
    resources: {
      en: {
        translation: {
          'pit.currency.carrotAmount': '{{value}} carrots',
          'pit.currency.whiteAmount': '{{value}} white radishes',
        },
      },
    },
  });

  assert.equal(formatCoinNumber('9007199254740993', 'en'), '9,007,199,254,740,993');
  assert.equal(addCoinValues('9007199254740993', '7'), '9007199254741000');
  assert.equal(subtractCoinValues('9007199254741000', '7'), '9007199254740993');
  assert.equal(compareCoinValues('9007199254740993', '9007199254740992'), 1);
  assert.equal(absoluteCoinValue('-9007199254740993'), '9007199254740993');
  assert.equal(formatCoinAmount('1234', 'en', instance.t, 'white'), '1.234 white radishes');
  assert.equal(formatCoinAmount('1234', 'zh', instance.t, 'carrot', false), '1,234');
});

test('交易方向应由稳定用户 ID 关系解析，转账金额不依赖显示字段或正负号', () => {
  const transaction = createTransaction();

  assert.equal(resolveTransactionDirection(transaction, '10001'), 'out');
  assert.equal(resolveTransactionDirection(transaction, '20002'), 'in');
  assert.equal(getSignedTransactionAmount(transaction, '10001'), '-9007199254740993');
  assert.equal(getSignedTransactionAmount(transaction, '20002'), '9007199254740993');
});

test('交易、状态与统计分类只按稳定词元本地化，未知词元保留原值', async () => {
  const instance = i18next.createInstance();
  await instance.init({
    lng: 'en',
    resources: {
      en: {
        translation: {
          'pit.transactionType.TRANSFER': 'Transfer',
          'pit.transactionStatus.SUCCESS': 'Succeeded',
          'pit.statistics.category.OUT_TRANSFER': 'Transfer spending',
        },
      },
    },
  });

  assert.equal(formatTransactionType('TRANSFER', instance.t), 'Transfer');
  assert.equal(formatTransactionStatus('SUCCESS', instance.t), 'Succeeded');
  assert.equal(formatStatisticsCategory('OUT_TRANSFER', instance.t), 'Transfer spending');
  assert.equal(formatTransactionType('  CUSTOM_REWARD  ', instance.t), 'CUSTOM_REWARD');
});

test('萝卜域日期与图表日期应按 locale 和指定时区格式化', () => {
  assert.equal(
    formatCoinDateTime('2026-07-15T04:30:00.000Z', 'Asia/Shanghai', 'zh'),
    '2026-07-15 12:30:00',
  );
  assert.match(
    formatCoinDateTime('2026-07-15T04:30:00.000Z', 'Asia/Shanghai', 'en'),
    /07\/15\/2026, 12:30:00/,
  );
  assert.equal(formatCoinChartDate('2026-07-15', 'en'), '07/15');
});
