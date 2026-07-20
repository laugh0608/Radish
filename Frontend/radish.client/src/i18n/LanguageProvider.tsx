import type { PropsWithChildren } from 'react';
import { useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { LANGUAGE_STORAGE_KEY, getIntlLocale, normalizeLanguage } from '@/locales/language';

export function LanguageProvider({ children }: PropsWithChildren) {
  const { i18n } = useTranslation();
  const language = normalizeLanguage(i18n.resolvedLanguage ?? i18n.language) ?? 'zh';

  useEffect(() => {
    document.documentElement.lang = getIntlLocale(language);
  }, [language]);

  useEffect(() => {
    const handleStorage = (event: StorageEvent) => {
      if (event.key !== LANGUAGE_STORAGE_KEY) {
        return;
      }

      const nextLanguage = normalizeLanguage(event.newValue);
      if (nextLanguage && nextLanguage !== normalizeLanguage(i18n.resolvedLanguage ?? i18n.language)) {
        void i18n.changeLanguage(nextLanguage);
      }
    };

    window.addEventListener('storage', handleStorage);
    return () => window.removeEventListener('storage', handleStorage);
  }, [i18n]);

  return children;
}
