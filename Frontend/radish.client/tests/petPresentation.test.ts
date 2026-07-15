import assert from 'node:assert/strict';
import test from 'node:test';
import {
  formatPetDateTime,
  formatPetNumber,
  formatPetSignedNumber,
  getCooldownDisplayParts,
  getPetLogStatDeltas,
  resolvePetActionTranslationKey,
  resolvePetActionAvailability,
  resolvePetGrowthStageTranslationKey,
  resolvePetLogMessageTranslationKey,
  resolvePetMoodTranslationKey,
  resolvePetStatLevel,
  resolvePetStatusInsight,
} from '../src/pet/petPresentation.ts';

test('宠物展示值应按当前语言格式化数字和日期', () => {
  assert.equal(formatPetNumber('12345', 'zh'), '12,345');
  assert.equal(formatPetNumber('9007199254740993', 'en'), '9,007,199,254,740,993');
  assert.equal(formatPetNumber('', 'en'), '-');
  assert.equal(formatPetNumber('invalid', 'en'), '-');
  assert.equal(formatPetSignedNumber(12345, 'en'), '+12,345');
  assert.equal(formatPetSignedNumber('-12345', 'en'), '-12,345');

  const value = '2026-07-15T04:30:00.000Z';
  assert.equal(formatPetDateTime(value, 'Asia/Shanghai', 'zh'), '2026-07-15 12:30:00');
  assert.match(formatPetDateTime(value, 'Asia/Shanghai', 'en'), /07\/15\/2026, 12:30:00/);
});

test('宠物系统派生内容应从稳定字段映射本地词元', () => {
  assert.equal(resolvePetGrowthStageTranslationKey(1), 'pet.growthStage.sprout');
  assert.equal(resolvePetGrowthStageTranslationKey(4), 'pet.growthStage.mature');
  assert.equal(resolvePetMoodTranslationKey('hungry'), 'pet.mood.hungry');
  assert.equal(resolvePetMoodTranslationKey('unknown'), 'pet.mood.calm');
  assert.equal(resolvePetActionTranslationKey('feed'), 'pet.care.action.feed');
  assert.equal(resolvePetActionTranslationKey('unknown'), 'pet.care.action.unknown');
  assert.equal(resolvePetLogMessageTranslationKey('rest'), 'pet.logs.message.rest');
  assert.equal(resolvePetLogMessageTranslationKey('unknown'), 'pet.logs.message.unknown');
});

test('resolvePetStatusInsight 应按心情和状态值给出明确关注项', () => {
  assert.deepEqual(resolvePetStatusInsight({
    voMood: 'hungry',
    voSatiety: 20,
    voCleanliness: 90,
    voEnergy: 90,
  }).focusStatKey, 'voSatiety');

  assert.equal(resolvePetStatusInsight({
    voMood: 'unknown',
    voSatiety: 80,
    voCleanliness: 25,
    voEnergy: 90,
  }).intent, 'messy');

  assert.equal(resolvePetStatusInsight({
    voMood: 'unknown',
    voSatiety: 80,
    voCleanliness: 80,
    voEnergy: 80,
  }).intent, 'thriving');
});

test('resolvePetStatLevel 应区分良好、关注和偏低状态', () => {
  assert.equal(resolvePetStatLevel(80), 'good');
  assert.equal(resolvePetStatLevel(45), 'watch');
  assert.equal(resolvePetStatLevel(12), 'critical');
});

test('resolvePetActionAvailability 应优先区分次数耗尽、冷却和可用', () => {
  const now = Date.parse('2026-06-15T12:00:00.000Z');

  assert.equal(resolvePetActionAvailability({
    voActionType: 'feed',
    voActionName: '喂食',
    voDailyLimit: 3,
    voUsedToday: 3,
    voRemainingToday: 0,
    voNextAvailableAt: '2026-06-15T12:20:00.000Z',
    voCanUse: false,
  }, now).kind, 'usedUp');

  const cooldown = resolvePetActionAvailability({
    voActionType: 'clean',
    voActionName: '清洁',
    voDailyLimit: 3,
    voUsedToday: 1,
    voRemainingToday: 2,
    voNextAvailableAt: '2026-06-15T12:20:00.000Z',
    voCanUse: false,
  }, now);
  assert.equal(cooldown.kind, 'cooldown');
  assert.equal(cooldown.cooldownMs, 20 * 60 * 1000);

  assert.equal(resolvePetActionAvailability({
    voActionType: 'play',
    voActionName: '互动',
    voDailyLimit: 3,
    voUsedToday: 0,
    voRemainingToday: 3,
    voNextAvailableAt: null,
    voCanUse: true,
  }, now).kind, 'ready');
});

test('getCooldownDisplayParts 应把剩余时间转换为用户可读粒度', () => {
  assert.deepEqual(getCooldownDisplayParts(61 * 1000), { value: 2, unit: 'minute' });
  assert.deepEqual(getCooldownDisplayParts(75 * 60 * 1000), { value: 2, unit: 'hour' });
  assert.equal(getCooldownDisplayParts(0), null);
});

test('getPetLogStatDeltas 应只返回发生变化的状态项', () => {
  assert.deepEqual(getPetLogStatDeltas({
    voId: '1',
    voPetProfileId: '2',
    voPetPublicId: 'pet_1',
    voActionType: 'feed',
    voActionName: '喂食',
    voBeforeSatiety: 70,
    voAfterSatiety: 94,
    voBeforeCleanliness: 70,
    voAfterCleanliness: 70,
    voBeforeEnergy: 70,
    voAfterEnergy: 66,
    voGrowthDelta: 5,
    voMessage: '小萝卜吃饱了一些。',
    voCreateTime: '2026-06-15T12:00:00.000Z',
  }), [
    { statKey: 'voSatiety', value: 24 },
    { statKey: 'voEnergy', value: -4 },
  ]);
});
