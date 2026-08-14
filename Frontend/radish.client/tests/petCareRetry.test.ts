import assert from 'node:assert/strict';
import test from 'node:test';
import type { PetCareActionState, PetProfile, PetStatLog } from '../src/api/pet.ts';
import {
  hasAuthoritativePetCareAdvance,
  isAmbiguousPetCareError,
} from '../src/pet/petCareRetry.ts';

function createAction(overrides: Partial<PetCareActionState> = {}): PetCareActionState {
  return {
    voActionType: 'feed',
    voActionName: '喂食',
    voDailyLimit: 3,
    voUsedToday: 0,
    voRemainingToday: 3,
    voNextAvailableAt: null,
    voCanUse: true,
    ...overrides,
  };
}

function createPet(action: PetCareActionState): PetProfile {
  return {
    voId: '1',
    voPublicId: 'pet_1',
    voUserId: '2',
    voName: '小萝卜',
    voSpeciesKey: 'radish',
    voShapeKey: 'sprout',
    voGrowthStage: 1,
    voGrowthStageName: '幼苗',
    voMood: 'calm',
    voMoodDisplay: '平静',
    voSatiety: 70,
    voCleanliness: 70,
    voEnergy: 70,
    voGrowthValue: '10',
    voIsPublic: true,
    voCreateTime: '2026-08-10T00:00:00Z',
    voCareActions: [action],
  };
}

function createLog(id: string): PetStatLog {
  return {
    voId: id,
    voPetProfileId: '1',
    voPetPublicId: 'pet_1',
    voActionType: 'feed',
    voActionName: '喂食',
    voBeforeSatiety: 70,
    voAfterSatiety: 90,
    voBeforeCleanliness: 70,
    voAfterCleanliness: 70,
    voBeforeEnergy: 70,
    voAfterEnergy: 65,
    voGrowthDelta: '2',
    voMessage: '吃饱了一些。',
    voCreateTime: '2026-08-10T01:00:00Z',
  };
}

function createApiError(httpStatus?: number, statusCode?: number): Error {
  const error = new Error('failed');
  error.name = 'ApiResponseError';
  return Object.assign(error, { httpStatus, statusCode });
}

test('宠物照料只把传输不确定与可重试服务错误视为歧义结果', () => {
  assert.equal(isAmbiguousPetCareError(new Error('network disconnected')), true);
  assert.equal(isAmbiguousPetCareError(createApiError(503)), true);
  assert.equal(isAmbiguousPetCareError(createApiError(408)), true);
  assert.equal(isAmbiguousPetCareError(createApiError(400)), false);
  assert.equal(isAmbiguousPetCareError(createApiError(undefined, 409)), false);
});

test('宠物照料歧义复核只接受新增日志或权威次数状态推进', () => {
  const beforeAction = createAction();
  const beforePet = createPet(beforeAction);
  const beforeLogs = [createLog('10')];

  assert.equal(hasAuthoritativePetCareAdvance(
    beforePet,
    beforeLogs,
    createPet(beforeAction),
    [...beforeLogs, createLog('11')],
    'feed',
  ), true);

  assert.equal(hasAuthoritativePetCareAdvance(
    beforePet,
    beforeLogs,
    createPet(createAction({ voUsedToday: 1, voRemainingToday: 2, voCanUse: false })),
    beforeLogs,
    'feed',
  ), true);

  assert.equal(hasAuthoritativePetCareAdvance(
    beforePet,
    beforeLogs,
    createPet(beforeAction),
    beforeLogs,
    'feed',
  ), false);
});
