import { apiGet, apiPost } from '@radish/http';
import type { UserBenefit, UserBenefitActionResult } from '@/types/shop';
import { useThemeStore } from '@/stores/themeStore';
import { tokenService } from '@/services/tokenService';
import {
  isBuiltinThemeId,
  type BuiltinThemeId,
  type EntitlementThemeId,
  type ThemeId,
} from './theme';
import {
  buildThemeEntitlementSnapshot,
  isThemeBenefitType,
  toOwnedThemeEntitlement,
  type ThemeEntitlementSnapshot,
} from './themeEntitlementSnapshot';

export function reconcileThemeBenefitActionResult(result: UserBenefitActionResult): void {
  if (!isThemeBenefitType(result.voBenefitType)) {
    return;
  }

  const currentEntitlement = result.voCurrentBenefit
    ? toOwnedThemeEntitlement(result.voCurrentBenefit)
    : null;
  useThemeStore.getState().setActiveEntitlement(
    currentEntitlement ? { ...currentEntitlement, active: true } : null,
  );
}

let refreshPromise: Promise<ThemeEntitlementSnapshot> | null = null;
let syncGeneration = 0;

export function invalidateThemeEntitlementSync(): void {
  syncGeneration += 1;
  refreshPromise = null;
}

async function requestThemeEntitlements(requestGeneration: number): Promise<ThemeEntitlementSnapshot> {
  const response = await apiGet<UserBenefit[]>('/api/v1/Shop/GetMyBenefits?includeExpired=false', {
    withAuth: true,
  });
  if (!response.ok || !response.data) {
    throw new Error(response.message || '无法读取主题权益');
  }

  const snapshot = buildThemeEntitlementSnapshot(response.data);
  if (requestGeneration === syncGeneration) {
    useThemeStore.getState().setEntitlementSnapshot(snapshot.owned);
  }
  return snapshot;
}

export async function refreshThemeEntitlements(force = false): Promise<ThemeEntitlementSnapshot> {
  if (refreshPromise) {
    if (!force) {
      return await refreshPromise;
    }

    await refreshPromise.catch(() => undefined);
  }

  const store = useThemeStore.getState();
  store.setEntitlementSyncing(true);
  store.setEntitlementError(null);
  const requestGeneration = syncGeneration;
  const pendingPromise = requestThemeEntitlements(requestGeneration)
    .catch(error => {
      if (requestGeneration !== syncGeneration) {
        return { owned: [] };
      }

      const message = error instanceof Error ? error.message : '无法读取主题权益';
      useThemeStore.getState().setEntitlementError(message);
      throw error;
    })
    .finally(() => {
      if (refreshPromise !== pendingPromise) {
        return;
      }

      useThemeStore.getState().setEntitlementSyncing(false);
      refreshPromise = null;
    });
  refreshPromise = pendingPromise;

  return await refreshPromise;
}

async function postThemeAction(path: string): Promise<UserBenefitActionResult> {
  const response = await apiPost<UserBenefitActionResult>(path, undefined, { withAuth: true });
  if (!response.ok || !response.data) {
    throw new Error(response.message || '主题切换失败');
  }

  reconcileThemeBenefitActionResult(response.data);
  return response.data;
}

async function selectBuiltinTheme(themeId: BuiltinThemeId): Promise<void> {
  const store = useThemeStore.getState();
  const activeEntitlement = store.activeEntitlement;
  if (activeEntitlement) {
    await postThemeAction(
      `/api/v1/Shop/DeactivateBenefit/${encodeURIComponent(String(activeEntitlement.benefitId))}`,
    );
  }

  useThemeStore.getState().setBuiltinTheme(themeId);
}

async function selectEntitlementTheme(themeId: EntitlementThemeId): Promise<void> {
  const store = useThemeStore.getState();
  const entitlement = store.ownedEntitlements.find(item => item.themeId === themeId);
  if (!entitlement) {
    throw new Error('尚未拥有该主题权益');
  }

  if (store.activeEntitlement?.themeId === themeId) {
    return;
  }

  await postThemeAction(
    `/api/v1/Shop/ActivateBenefit/${encodeURIComponent(String(entitlement.benefitId))}`,
  );
}

export async function selectTheme(themeId: ThemeId): Promise<void> {
  const store = useThemeStore.getState();
  store.setEntitlementSyncing(true);
  store.setEntitlementError(null);

  try {
    if (isBuiltinThemeId(themeId)) {
      await selectBuiltinTheme(themeId);
    } else {
      await selectEntitlementTheme(themeId);
    }

    if (!tokenService.getAccessToken()) {
      return;
    }

    await refreshThemeEntitlements(true);
  } catch (error) {
    const message = error instanceof Error ? error.message : '主题切换失败';
    useThemeStore.getState().setEntitlementError(message);
    throw error;
  } finally {
    useThemeStore.getState().setEntitlementSyncing(false);
  }
}
