// 基于 ABP 的本地化 Provider：优先读取后端资源，缺失时回退到本地字典
import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { fetchAbpLocalization } from './abpApi'
import { I18nContext, type Locale } from './I18nContext'
import { messages as fallbackMessages } from './messages'

const STORAGE_KEY = 'app.locale'
const DEFAULT_RESOURCE = 'Radish'

function resolveInitialLocale(): Locale {
  const stored = typeof window !== 'undefined' ? (localStorage.getItem(STORAGE_KEY) as Locale | null) : null
  if (stored === 'en' || stored === 'zh-Hans') return stored
  const nav = typeof navigator !== 'undefined' ? navigator.language : 'en'
  return nav.toLowerCase().startsWith('zh') ? 'zh-Hans' : 'en'
}

type ResourceMap = Record<string, Record<string, string>>

export function I18nProvider({ children }: { children: React.ReactNode }) {
  const [locale, setLocaleState] = useState<Locale>(resolveInitialLocale())
  const [resources, setResources] = useState<ResourceMap>({})
  const cache = useRef<Record<string, ResourceMap>>({})
  const [ready, setReady] = useState(false)

  // 拉取 ABP 资源（按语言缓存）
  const load = useCallback(async (l: Locale) => {
    setReady(false)
    if (cache.current[l]) {
      setResources(cache.current[l])
      setReady(true)
      return
    }
    try {
      const loc = await fetchAbpLocalization(l)
      const values = loc.values || {}
      cache.current[l] = values
      setResources(values)
    } catch {
      // 拉取失败时回退为空（由本地字典兜底）
      setResources({})
    } finally {
      setReady(true)
    }
  }, [])

  useEffect(() => {
    load(locale)
    try { localStorage.setItem(STORAGE_KEY, locale) } catch {}
  }, [locale, load])

  const setLocale = useCallback((l: Locale) => {
    if (l !== 'en' && l !== 'zh-Hans') return
    setLocaleState(l)
  }, [])

  const t = useCallback((key: string) => {
    // 支持 `Resource::Key` 与 `::Key`（默认 Radish）
    let res = DEFAULT_RESOURCE
    let realKey = key
    const idx = key.indexOf('::')
    if (idx >= 0) {
      const left = key.slice(0, idx)
      realKey = key.slice(idx + 2)
      if (left) res = left
    }
    const dict = resources[res]
    const fromServer = dict?.[realKey]
    if (fromServer !== undefined) return fromServer

    // 本地兜底（保持现有 UI 正常）
    const local = fallbackMessages[locale as 'en' | 'zh-Hans'] as Record<string, string>
    return local?.[key] ?? realKey
  }, [resources, locale])

  const value = useMemo(() => ({ locale, setLocale, t }), [locale, setLocale, t])
  return <I18nContext.Provider value={value}>{ready ? children : null}</I18nContext.Provider>
}
