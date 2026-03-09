import { createContext, useContext, type ReactNode } from 'react';
import type { WindowState } from '@/desktop/types';

const CurrentWindowContext = createContext<WindowState | null>(null);

interface CurrentWindowProviderProps {
  value: WindowState;
  children: ReactNode;
}

export const CurrentWindowProvider = ({ value, children }: CurrentWindowProviderProps) => {
  return <CurrentWindowContext.Provider value={value}>{children}</CurrentWindowContext.Provider>;
};

export function useCurrentWindow(): WindowState | null {
  return useContext(CurrentWindowContext);
}
