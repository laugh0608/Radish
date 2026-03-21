import { useContext } from 'react';
import type { WindowState } from '@/desktop/types';
import { CurrentWindowContext } from './CurrentWindowContextValue';

export function useCurrentWindow(): WindowState | null {
  return useContext(CurrentWindowContext);
}
