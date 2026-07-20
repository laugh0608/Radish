import { create } from 'zustand';
import {
  applyTheme,
  builtinThemeIds,
  readStoredBuiltinTheme,
  readStoredTheme,
  writeStoredBuiltinTheme,
  writeStoredTheme,
  type BuiltinThemeId,
  type ThemeId,
} from '@/theme/theme';
import type { OwnedThemeEntitlement } from '@/theme/themeEntitlementSnapshot';

interface ThemeStore {
  theme: ThemeId;
  builtinTheme: BuiltinThemeId;
  ownedEntitlements: OwnedThemeEntitlement[];
  activeEntitlement: OwnedThemeEntitlement | null;
  isSyncingEntitlements: boolean;
  entitlementError: string | null;
  setBuiltinTheme: (theme: BuiltinThemeId) => void;
  setEntitlementSnapshot: (owned: OwnedThemeEntitlement[]) => void;
  setActiveEntitlement: (active: OwnedThemeEntitlement | null) => void;
  clearEntitlements: () => void;
  setEntitlementSyncing: (value: boolean) => void;
  setEntitlementError: (message: string | null) => void;
  syncStoredPreferences: () => void;
  getNextBuiltinTheme: () => BuiltinThemeId;
}

function applyEffectiveTheme(theme: ThemeId): void {
  writeStoredTheme(theme);
  applyTheme(theme);
}

export const useThemeStore = create<ThemeStore>((set, get) => ({
  theme: readStoredTheme(),
  builtinTheme: readStoredBuiltinTheme(),
  ownedEntitlements: [],
  activeEntitlement: null,
  isSyncingEntitlements: false,
  entitlementError: null,

  setBuiltinTheme: (builtinTheme) => {
    writeStoredBuiltinTheme(builtinTheme);
    const activeEntitlement = get().activeEntitlement;
    const theme = activeEntitlement?.themeId ?? builtinTheme;
    applyEffectiveTheme(theme);
    set({ builtinTheme, theme, entitlementError: null });
  },

  setEntitlementSnapshot: (ownedEntitlements) => {
    const activeEntitlement = ownedEntitlements.find(item => item.active) ?? null;
    const theme = activeEntitlement?.themeId ?? get().builtinTheme;
    applyEffectiveTheme(theme);
    set({
      ownedEntitlements,
      activeEntitlement,
      theme,
      entitlementError: null,
    });
  },

  setActiveEntitlement: (activeEntitlement) => {
    const ownedEntitlements = get().ownedEntitlements.map(item => ({
      ...item,
      active: activeEntitlement?.benefitId === item.benefitId,
    }));
    if (activeEntitlement && !ownedEntitlements.some(item => item.benefitId === activeEntitlement.benefitId)) {
      ownedEntitlements.push(activeEntitlement);
    }
    const theme = activeEntitlement?.themeId ?? get().builtinTheme;
    applyEffectiveTheme(theme);
    set({ activeEntitlement, ownedEntitlements, theme, entitlementError: null });
  },

  clearEntitlements: () => {
    const theme = get().builtinTheme;
    applyEffectiveTheme(theme);
    set({
      theme,
      ownedEntitlements: [],
      activeEntitlement: null,
      isSyncingEntitlements: false,
      entitlementError: null,
    });
  },

  setEntitlementSyncing: (isSyncingEntitlements) => set({ isSyncingEntitlements }),
  setEntitlementError: (entitlementError) => set({ entitlementError }),

  syncStoredPreferences: () => {
    const builtinTheme = readStoredBuiltinTheme();
    if (get().activeEntitlement) {
      set({ builtinTheme });
      return;
    }

    applyTheme(builtinTheme);
    set({ builtinTheme, theme: builtinTheme });
  },

  getNextBuiltinTheme: () => {
    const currentTheme = get().builtinTheme;
    const currentIndex = builtinThemeIds.indexOf(currentTheme);
    return builtinThemeIds[(currentIndex + 1) % builtinThemeIds.length] ?? builtinThemeIds[0];
  },
}));
