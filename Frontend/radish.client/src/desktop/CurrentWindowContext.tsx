import type { ReactNode } from 'react';
import type { WindowState } from '@/desktop/types';
import { CurrentWindowContext } from './CurrentWindowContextValue';

interface CurrentWindowProviderProps {
  value: WindowState;
  children: ReactNode;
}

export const CurrentWindowProvider = ({ value, children }: CurrentWindowProviderProps) => {
  return <CurrentWindowContext.Provider value={value}>{children}</CurrentWindowContext.Provider>;
};
