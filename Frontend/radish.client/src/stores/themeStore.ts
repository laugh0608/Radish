import { create } from 'zustand';
import { applyTheme, readStoredTheme, themeOptions, writeStoredTheme, type ThemeId } from '@/theme/theme';

interface ThemeStore {
  theme: ThemeId;
  setTheme: (theme: ThemeId) => void;
  cycleTheme: () => void;
}

export const useThemeStore = create<ThemeStore>((set, get) => ({
  theme: readStoredTheme(),

  setTheme: (theme) => {
    writeStoredTheme(theme);
    applyTheme(theme);
    set({ theme });
  },

  cycleTheme: () => {
    const currentTheme = get().theme;
    const currentIndex = themeOptions.findIndex(item => item.id === currentTheme);
    const nextTheme = themeOptions[(currentIndex + 1) % themeOptions.length]?.id ?? themeOptions[0].id;
    get().setTheme(nextTheme);
  }
}));
