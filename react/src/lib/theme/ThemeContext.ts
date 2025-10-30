import { createContext } from 'react'

export type ThemeMode = 'light' | 'dark'

export type ThemeCtx = {
  theme: ThemeMode
  setTheme: (m: ThemeMode) => void
  toggleTheme: () => void
}

export const ThemeContext = createContext<ThemeCtx | null>(null)

