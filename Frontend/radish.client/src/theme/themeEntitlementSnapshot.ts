import type { UserBenefit } from '../types/shop.ts';
import { isEntitlementThemeId, type EntitlementThemeId } from './theme.ts';

export interface OwnedThemeEntitlement {
  benefitId: string | number;
  themeId: EntitlementThemeId;
  name: string;
  active: boolean;
}

export interface ThemeEntitlementSnapshot {
  owned: OwnedThemeEntitlement[];
}

export function isThemeBenefitType(value: unknown): boolean {
  const normalized = String(value ?? '').trim().toLowerCase();
  return normalized === '4' || normalized === 'theme';
}

function isActiveStatus(value: unknown): boolean {
  const normalized = String(value ?? '').trim().toLowerCase();
  return normalized === '1' || normalized === 'active';
}

function isUnavailableStatus(value: unknown): boolean {
  const normalized = String(value ?? '').trim().toLowerCase();
  return normalized === '2'
    || normalized === '3'
    || normalized === 'expired'
    || normalized === 'revoked';
}

export function toOwnedThemeEntitlement(benefit: UserBenefit): OwnedThemeEntitlement | null {
  if (!isThemeBenefitType(benefit.voBenefitType) || isUnavailableStatus(benefit.voStatus)) {
    return null;
  }

  const themeId = benefit.voBenefitValue?.trim();
  if (!isEntitlementThemeId(themeId)) {
    return null;
  }

  return {
    benefitId: benefit.voId,
    themeId,
    name: benefit.voBenefitName?.trim() || themeId,
    active: benefit.voIsActive === true || isActiveStatus(benefit.voStatus),
  };
}

export function buildThemeEntitlementSnapshot(benefits: UserBenefit[]): ThemeEntitlementSnapshot {
  const byThemeId = new Map<EntitlementThemeId, OwnedThemeEntitlement>();

  for (const benefit of benefits) {
    const entitlement = toOwnedThemeEntitlement(benefit);
    if (!entitlement) {
      continue;
    }

    const existing = byThemeId.get(entitlement.themeId);
    if (!existing || entitlement.active) {
      byThemeId.set(entitlement.themeId, entitlement);
    }
  }

  return {
    owned: [...byThemeId.values()],
  };
}
