export const THEME_STORAGE_KEY = 'radish_client_theme';
export const BUILTIN_THEME_STORAGE_KEY = 'radish_client_builtin_theme';

export const builtinThemeIds = ['default', 'guofeng'] as const;
export const entitlementThemeIds = ['theme-dark-night', 'theme-sakura'] as const;
export const themeIds = [...builtinThemeIds, ...entitlementThemeIds] as const;

export type BuiltinThemeId = (typeof builtinThemeIds)[number];
export type EntitlementThemeId = (typeof entitlementThemeIds)[number];
export type ThemeId = (typeof themeIds)[number];
export type ThemeAccess = 'builtin' | 'entitlement';

export interface ThemeDefinition {
  id: ThemeId;
  labelKey: string;
  access: ThemeAccess;
  colorScheme: 'light' | 'dark';
  themeConfig: {
    token: {
      colorPrimary: string;
      colorPrimaryHover: string;
      colorLink: string;
      colorLinkHover: string;
      colorBgBase: string;
      colorBgContainer: string;
      colorTextBase: string;
      colorBorder: string;
      colorSuccess: string;
      colorWarning: string;
      colorError: string;
      colorInfo: string;
    };
  };
}

export const DEFAULT_THEME: BuiltinThemeId = 'guofeng';

export const themeDefinitions: Record<ThemeId, ThemeDefinition> = {
  default: {
    id: 'default',
    labelKey: 'theme.default',
    access: 'builtin',
    colorScheme: 'light',
    themeConfig: {
      token: {
        colorPrimary: '#435c74',
        colorPrimaryHover: '#55738f',
        colorLink: '#435c74',
        colorLinkHover: '#55738f',
        colorBgBase: '#edf1f2',
        colorBgContainer: '#fbfcfc',
        colorTextBase: '#23313b',
        colorBorder: '#cfdadd',
        colorSuccess: '#4f8e77',
        colorWarning: '#aa761f',
        colorError: '#bb5b54',
        colorInfo: '#4d6a80',
      },
    },
  },
  guofeng: {
    id: 'guofeng',
    labelKey: 'theme.guofeng',
    access: 'builtin',
    colorScheme: 'light',
    themeConfig: {
      token: {
        colorPrimary: '#435c74',
        colorPrimaryHover: '#55738f',
        colorLink: '#435c74',
        colorLinkHover: '#55738f',
        colorBgBase: '#f4efe6',
        colorBgContainer: '#fbf7f0',
        colorTextBase: '#2f2a25',
        colorBorder: '#d8c9bb',
        colorSuccess: '#4f9c83',
        colorWarning: '#b5826d',
        colorError: '#c3564d',
        colorInfo: '#435c74',
      },
    },
  },
  'theme-dark-night': {
    id: 'theme-dark-night',
    labelKey: 'theme.darkNight',
    access: 'entitlement',
    colorScheme: 'dark',
    themeConfig: {
      token: {
        colorPrimary: '#8bb9ca',
        colorPrimaryHover: '#a1c7d5',
        colorLink: '#8bb9ca',
        colorLinkHover: '#a1c7d5',
        colorBgBase: '#0f171d',
        colorBgContainer: '#17232b',
        colorTextBase: '#e6edf1',
        colorBorder: '#344650',
        colorSuccess: '#72b39b',
        colorWarning: '#d5a95f',
        colorError: '#e07a72',
        colorInfo: '#8bb9ca',
      },
    },
  },
  'theme-sakura': {
    id: 'theme-sakura',
    labelKey: 'theme.sakura',
    access: 'entitlement',
    colorScheme: 'light',
    themeConfig: {
      token: {
        colorPrimary: '#596f88',
        colorPrimaryHover: '#6c839d',
        colorLink: '#596f88',
        colorLinkHover: '#6c839d',
        colorBgBase: '#fff3f6',
        colorBgContainer: '#fffafb',
        colorTextBase: '#3d2932',
        colorBorder: '#e8cbd5',
        colorSuccess: '#5f947f',
        colorWarning: '#ad7628',
        colorError: '#c65361',
        colorInfo: '#596f88',
      },
    },
  },
};

export const themeOptions = themeIds.map(id => themeDefinitions[id]);
export const builtinThemeOptions = builtinThemeIds.map(id => themeDefinitions[id]);

export function isThemeId(value: string | null | undefined): value is ThemeId {
  return typeof value === 'string' && themeIds.includes(value as ThemeId);
}

export function isBuiltinThemeId(value: string | null | undefined): value is BuiltinThemeId {
  return typeof value === 'string' && builtinThemeIds.includes(value as BuiltinThemeId);
}

export function isEntitlementThemeId(value: string | null | undefined): value is EntitlementThemeId {
  return typeof value === 'string' && entitlementThemeIds.includes(value as EntitlementThemeId);
}

export function resolveTheme(value: string | null | undefined): ThemeId {
  return isThemeId(value) ? value : DEFAULT_THEME;
}

export function resolveBuiltinTheme(value: string | null | undefined): BuiltinThemeId {
  return isBuiltinThemeId(value) ? value : DEFAULT_THEME;
}

function readStorageValue(key: string): string | null {
  if (typeof window === 'undefined') {
    return null;
  }

  try {
    return window.localStorage.getItem(key);
  } catch {
    return null;
  }
}

function writeStorageValue(key: string, value: string): void {
  if (typeof window === 'undefined') {
    return;
  }

  try {
    window.localStorage.setItem(key, value);
  } catch {
    // 浏览器禁用本地存储时仍保留当前内存主题。
  }
}

export function readStoredBuiltinTheme(): BuiltinThemeId {
  const storedBuiltinTheme = readStorageValue(BUILTIN_THEME_STORAGE_KEY);
  if (isBuiltinThemeId(storedBuiltinTheme)) {
    return storedBuiltinTheme;
  }

  const legacyTheme = readStorageValue(THEME_STORAGE_KEY);
  return resolveBuiltinTheme(legacyTheme);
}

export function readStoredTheme(): ThemeId {
  const storedTheme = readStorageValue(THEME_STORAGE_KEY);
  return isThemeId(storedTheme) ? storedTheme : readStoredBuiltinTheme();
}

export function writeStoredBuiltinTheme(theme: BuiltinThemeId): void {
  writeStorageValue(BUILTIN_THEME_STORAGE_KEY, theme);
}

export function writeStoredTheme(theme: ThemeId): void {
  writeStorageValue(THEME_STORAGE_KEY, theme);
}

export function applyTheme(theme: ThemeId): void {
  if (typeof document === 'undefined') {
    return;
  }

  const definition = themeDefinitions[theme];
  document.documentElement.dataset.theme = theme;
  document.documentElement.dataset.rdTheme = definition.colorScheme;
  document.documentElement.style.colorScheme = definition.colorScheme;
}

export function initializeTheme(): ThemeId {
  const theme = readStoredTheme();
  applyTheme(theme);
  return theme;
}
