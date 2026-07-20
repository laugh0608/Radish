import { useTranslation } from 'react-i18next';
import { themeOptions } from './theme';
import { useThemeStore } from '@/stores/themeStore';
import { selectTheme } from './themeEntitlements';
import { log } from '@/utils/logger';

export function useTheme() {
  const { t } = useTranslation();
  const theme = useThemeStore(state => state.theme);
  const getNextBuiltinTheme = useThemeStore(state => state.getNextBuiltinTheme);

  const options = themeOptions.map(item => ({
    ...item,
    label: t(item.labelKey)
  }));

  const currentTheme = options.find(item => item.id === theme) ?? options[0];

  return {
    theme,
    currentTheme,
    options,
    setTheme: selectTheme,
    cycleTheme: () => {
      void selectTheme(getNextBuiltinTheme()).catch(error => {
        log.warn('Theme', '切换内建主题失败', error);
      });
    }
  };
}
