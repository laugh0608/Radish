import assert from 'node:assert/strict';
import test from 'node:test';
import i18next from 'i18next';
import {
  addExperienceValues,
  buildExperienceBarData,
  buildExperienceDailyStats,
  buildExperienceSourceStats,
  formatExperienceDateTime,
  formatExperienceNumber,
  formatExperiencePercent,
  formatExperienceSignedNumber,
  formatExperienceType,
  resolveExperienceTypeTranslationKey,
} from '../src/experience/experiencePresentation.ts';
import type { ExperienceData, ExpTransactionData } from '../src/api/experience.ts';

const experience: ExperienceData = {
  voUserId: '10001',
  voCurrentLevel: 3,
  voCurrentLevelName: '青瓷旅者',
  voCurrentExp: '1200',
  voTotalExp: '12345',
  voExpToNextLevel: '300',
  voNextLevel: 4,
  voNextLevelName: '云山笔客',
  voLevelProgress: 0.8,
  voThemeColor: '#587786',
  voExpFrozen: false,
};

function createTransaction(overrides: Partial<ExpTransactionData> = {}): ExpTransactionData {
  return {
    voId: '70001',
    voUserId: '10001',
    voExpType: 'POST_CREATE',
    voExpTypeDisplay: '不应消费的展示文案',
    voExpAmount: 20,
    voExpBefore: '1000',
    voExpAfter: '1020',
    voLevelBefore: 3,
    voLevelAfter: 3,
    voIsLevelUp: false,
    voCreateTime: '2026-07-15T04:30:00.000Z',
    ...overrides,
  };
}

test('经验系统词元应只按稳定 voExpType 解析，未知类型保留原值', async () => {
  const instance = i18next.createInstance();
  await instance.init({
    lng: 'en',
    resources: {
      en: {
        translation: {
          'experience.type.POST_CREATE': 'Post published',
        },
      },
    },
  });

  assert.equal(resolveExperienceTypeTranslationKey('POST_CREATE'), 'experience.type.POST_CREATE');
  assert.equal(resolveExperienceTypeTranslationKey('UNKNOWN_REWARD'), null);
  assert.equal(formatExperienceType('POST_CREATE', instance.t), 'Post published');
  assert.equal(formatExperienceType('  UNKNOWN_REWARD  ', instance.t), 'UNKNOWN_REWARD');

  const sources = buildExperienceSourceStats([
    createTransaction(),
    createTransaction({ voId: '70002', voExpType: 'UNKNOWN_REWARD', voExpAmount: 5 }),
  ], instance.t);
  assert.deepEqual(sources, [
    { type: 'POST_CREATE', name: 'Post published', value: 20 },
    { type: 'UNKNOWN_REWARD', name: 'UNKNOWN_REWARD', value: 5 },
  ]);
});

test('经验数字、日期、百分比和经验条上限应按 locale 与 long 字符串安全格式化', () => {
  assert.equal(formatExperienceNumber('9007199254740993', 'en'), '9,007,199,254,740,993');
  assert.equal(formatExperienceSignedNumber(12345, 'en'), '+12,345');
  assert.equal(formatExperiencePercent(0.876, 'en'), '87.6%');
  assert.equal(formatExperienceDateTime(
    '2026-07-15T04:30:00.000Z',
    'Asia/Shanghai',
    'zh',
  ), '2026-07-15 12:30:00');
  assert.match(formatExperienceDateTime(
    '2026-07-15T04:30:00.000Z',
    'Asia/Shanghai',
    'en',
  ), /07\/15\/2026, 12:30:00/);
  assert.equal(addExperienceValues('9007199254740993', '7'), '9007199254741000');
  assert.equal(buildExperienceBarData(experience).voNextLevelExp, '1500');
});

test('经验趋势图应按展示时区分组并按当前 locale 生成日期标签', () => {
  const transactions = [
    createTransaction({ voCreateTime: '2026-07-14T16:30:00.000Z', voExpAmount: 10 }),
    createTransaction({ voId: '70002', voCreateTime: '2026-07-15T02:00:00.000Z', voExpAmount: 20 }),
  ];
  const now = new Date('2026-07-15T04:00:00.000Z');

  assert.deepEqual(buildExperienceDailyStats(transactions, 2, now, 'zh', 'Asia/Shanghai'), [
    { date: '07/14', exp: 0 },
    { date: '07/15', exp: 30 },
  ]);
  assert.deepEqual(buildExperienceDailyStats(transactions, 2, now, 'en', 'UTC'), [
    { date: '07/14', exp: 10 },
    { date: '07/15', exp: 20 },
  ]);
});

test('等级名称、备注和冻结原因属于配置或人工原文', () => {
  assert.equal(experience.voCurrentLevelName, '青瓷旅者');
  const transaction = createTransaction({ voRemark: '人工复核后补发' });
  assert.equal(transaction.voRemark, '人工复核后补发');
});
