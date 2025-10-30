import { useCallback, useEffect, useMemo, useState } from 'react'
import { ThemeContext, type ThemeMode } from './ThemeContext'

const STORAGE_KEY = 'app.theme'

function resolveInitialTheme(): ThemeMode {
  if (typeof window === 'undefined') return 'light'
  const saved = localStorage.getItem(STORAGE_KEY) as ThemeMode | null
  if (saved === 'light' || saved === 'dark') return saved
  try {
    return window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light'
  } catch {
    return 'light'
  }
}

export function ThemeProvider({ children }: { children: React.ReactNode }) {
  const [theme, setThemeState] = useState<ThemeMode>(resolveInitialTheme())

  const setTheme = useCallback((m: ThemeMode) => setThemeState(m), [])
  const toggleTheme = useCallback(() => setThemeState((p) => (p === 'light' ? 'dark' : 'light')), [])

  useEffect(() => {
    if (typeof document === 'undefined') return
    const el = document.documentElement
    el.setAttribute('data-theme', theme)
    try {
      localStorage.setItem(STORAGE_KEY, theme)
    } catch {
      // ignore
    }
  }, [theme])

  const value = useMemo(() => ({ theme, setTheme, toggleTheme }), [theme, setTheme, toggleTheme])
  return <ThemeContext.Provider value={value}>{children}</ThemeContext.Provider>
}

