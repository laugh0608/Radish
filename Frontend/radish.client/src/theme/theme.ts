export const THEME_STORAGE_KEY = 'radish_client_theme';

export const themeIds = ['default', 'guofeng'] as const;

export type ThemeId = (typeof themeIds)[number];

export interface ThemeOption {
  id: ThemeId;
  labelKey: string;
}

export const DEFAULT_THEME: ThemeId = 'guofeng';

export const themeOptions: ThemeOption[] = [
  { id: 'default', labelKey: 'theme.default' },
  { id: 'guofeng', labelKey: 'theme.guofeng' }
];

export function isThemeId(value: string | null | undefined): value is ThemeId {
  return typeof value === 'string' && themeIds.includes(value as ThemeId);
}

export function resolveTheme(value: string | null | undefined): ThemeId {
  return isThemeId(value) ? value : DEFAULT_THEME;
}

export function readStoredTheme(): ThemeId {
  if (typeof window === 'undefined') {
    return DEFAULT_THEME;
  }

  return resolveTheme(window.localStorage.getItem(THEME_STORAGE_KEY));
}

export function writeStoredTheme(theme: ThemeId): void {
  if (typeof window === 'undefined') {
    return;
  }

  window.localStorage.setItem(THEME_STORAGE_KEY, theme);
}

export function applyTheme(theme: ThemeId): void {
  if (typeof document === 'undefined') {
    return;
  }

  document.documentElement.dataset.theme = theme;
  document.documentElement.style.colorScheme = 'light';
}

export function initializeTheme(): ThemeId {
  const theme = readStoredTheme();
  applyTheme(theme);
  return theme;
}
