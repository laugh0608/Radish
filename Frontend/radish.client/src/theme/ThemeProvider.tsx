import type { PropsWithChildren } from 'react';
import { useEffect } from 'react';
import { applyTheme } from './theme';
import { useThemeStore } from '@/stores/themeStore';

export const ThemeProvider = ({ children }: PropsWithChildren) => {
  const theme = useThemeStore(state => state.theme);

  useEffect(() => {
    applyTheme(theme);
  }, [theme]);

  return <>{children}</>;
};
