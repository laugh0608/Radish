import { antdLocales, ThemeProvider as SharedThemeProvider } from '@radish/ui';
import type { PropsWithChildren } from 'react';
import { useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { useAuthStore } from '@/stores/authStore';
import { useThemeStore } from '@/stores/themeStore';
import { tokenService } from '@/services/tokenService';
import { log } from '@/utils/logger';
import {
  BUILTIN_THEME_STORAGE_KEY,
  THEME_STORAGE_KEY,
  isEntitlementThemeId,
  themeDefinitions,
} from './theme';
import {
  invalidateThemeEntitlementSync,
  refreshThemeEntitlements,
} from './themeEntitlements';
import { normalizeLanguage } from '@/locales/language';

export const ThemeProvider = ({ children }: PropsWithChildren) => {
  const { i18n } = useTranslation();
  const theme = useThemeStore(state => state.theme);
  const isAuthenticated = useAuthStore(state => state.isAuthenticated);
  const definition = themeDefinitions[theme];
  const language = normalizeLanguage(i18n.resolvedLanguage ?? i18n.language) ?? 'zh';

  useEffect(() => {
    if (isAuthenticated) {
      void refreshThemeEntitlements().catch(error => {
        log.warn('Theme', '同步主题权益失败，保留最近有效主题', error);
      });
      return;
    }

    if (!tokenService.getAccessToken()) {
      invalidateThemeEntitlementSync();
      useThemeStore.getState().clearEntitlements();
    }
  }, [isAuthenticated]);

  useEffect(() => {
    const handleStorage = (event: StorageEvent) => {
      if (event.key !== THEME_STORAGE_KEY && event.key !== BUILTIN_THEME_STORAGE_KEY) {
        return;
      }

      useThemeStore.getState().syncStoredPreferences();
      if (isAuthenticated && (event.key === THEME_STORAGE_KEY || isEntitlementThemeId(event.newValue))) {
        void refreshThemeEntitlements().catch(error => {
          log.warn('Theme', '跨标签页同步主题权益失败', error);
        });
      }
    };

    window.addEventListener('storage', handleStorage);
    return () => window.removeEventListener('storage', handleStorage);
  }, [isAuthenticated]);

  return (
    <SharedThemeProvider
      dark={definition.colorScheme === 'dark'}
      themeConfig={definition.themeConfig}
      locale={antdLocales[language]}
    >
      {children}
    </SharedThemeProvider>
  );
};
