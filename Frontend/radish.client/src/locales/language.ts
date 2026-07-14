export const LANGUAGE_STORAGE_KEY = 'radish_lang';

export const supportedLanguages = ['zh', 'en'] as const;

export type SupportedLanguage = (typeof supportedLanguages)[number];

export function normalizeLanguage(value: string | null | undefined): SupportedLanguage | null {
  const normalized = value?.trim().toLowerCase();
  if (!normalized) {
    return null;
  }

  if (normalized === 'zh' || normalized.startsWith('zh-')) {
    return 'zh';
  }

  if (normalized === 'en' || normalized.startsWith('en-')) {
    return 'en';
  }

  return null;
}

export function getIntlLocale(language: string | null | undefined): 'zh-CN' | 'en-US' {
  return normalizeLanguage(language) === 'en' ? 'en-US' : 'zh-CN';
}
