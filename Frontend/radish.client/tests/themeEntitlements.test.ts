import assert from 'node:assert/strict';
import test from 'node:test';
import type { UserBenefit } from '../src/types/shop.ts';
import { buildThemeEntitlementSnapshot } from '../src/theme/themeEntitlementSnapshot.ts';

function createThemeBenefit(overrides: Partial<UserBenefit>): UserBenefit {
  return {
    voId: 1,
    voUserId: 9527,
    voBenefitType: 'Theme',
    voBenefitValue: 'theme-sakura',
    voBenefitName: '樱花主题',
    voSourceType: 'Purchase',
    voDurationType: 'Permanent',
    voStatus: 'Available',
    voCanActivate: true,
    voCanDeactivate: false,
    ...overrides,
  };
}

test('主题权益快照应只保留可用且已注册的 Theme 资源', () => {
  const snapshot = buildThemeEntitlementSnapshot([
    createThemeBenefit({ voId: 11, voBenefitValue: 'theme-sakura' }),
    createThemeBenefit({ voId: 12, voBenefitValue: 'theme-retired' }),
    createThemeBenefit({ voId: 13, voBenefitValue: 'theme-dark-night', voStatus: 'Expired' }),
    createThemeBenefit({ voId: 14, voBenefitType: 'Title', voBenefitValue: '称号' }),
  ]);

  assert.deepEqual(snapshot.owned.map(item => item.themeId), ['theme-sakura']);
});

test('同一主题存在多份权益时应优先保留服务端激活选择', () => {
  const snapshot = buildThemeEntitlementSnapshot([
    createThemeBenefit({ voId: 21, voBenefitValue: 'theme-sakura' }),
    createThemeBenefit({
      voId: 22,
      voBenefitValue: 'theme-sakura',
      voStatus: 1,
      voIsActive: true,
    }),
  ]);

  assert.equal(snapshot.owned.length, 1);
  assert.equal(snapshot.owned[0]?.benefitId, 22);
  assert.equal(snapshot.owned[0]?.active, true);
});
