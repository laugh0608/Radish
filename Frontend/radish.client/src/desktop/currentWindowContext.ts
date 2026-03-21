import { createContext } from 'react';
import type { WindowState } from '@/desktop/types';

export const CurrentWindowContext = createContext<WindowState | null>(null);
