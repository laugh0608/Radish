import { useTranslation } from 'react-i18next';
import { themeOptions } from './theme';
import { useThemeStore } from '@/stores/themeStore';

export function useTheme() {
  const { t } = useTranslation();
  const theme = useThemeStore(state => state.theme);
  const setTheme = useThemeStore(state => state.setTheme);
  const cycleTheme = useThemeStore(state => state.cycleTheme);

  const options = themeOptions.map(item => ({
    ...item,
    label: t(item.labelKey)
  }));

  const currentTheme = options.find(item => item.id === theme) ?? options[0];

  return {
    theme,
    currentTheme,
    options,
    setTheme,
    cycleTheme
  };
}
