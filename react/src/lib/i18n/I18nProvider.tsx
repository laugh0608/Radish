// 轻量本地化 Provider：提供 t(key) 翻译与语言切换
import { useCallback, useEffect, useMemo, useState } from 'react'
import { messages, SUPPORTED_LOCALES, type Locale } from './messages'
import { I18nContext } from './I18nContext'

// Ctx 类型定义移动至 I18nContext.ts，便于分离导出

const STORAGE_KEY = 'app.locale'

// 根据浏览器默认值推断语言（仅首次）
function resolveInitialLocale(): Locale {
  // 尝试从 localStorage 恢复
  const stored = typeof window !== 'undefined' ? (localStorage.getItem(STORAGE_KEY) as Locale | null) : null
  if (stored && SUPPORTED_LOCALES.includes(stored)) return stored

  // 根据浏览器语言猜测
  const nav = typeof navigator !== 'undefined' ? navigator.language : 'en'
  // 简单匹配到支持的语言
  const guess: Locale = nav.toLowerCase().startsWith('zh') ? 'zh-CN' : 'en'
  return guess
}

export function I18nProvider({ children }: { children: React.ReactNode }) {
  // 语言状态：默认从缓存或浏览器语言推断
  const [locale, setLocaleState] = useState<Locale>(resolveInitialLocale())

  // 写入 localStorage 以便记住选择
  useEffect(() => {
    try {
      localStorage.setItem(STORAGE_KEY, locale)
    } catch {
      // 忽略持久化失败
    }
  }, [locale])

  const setLocale = useCallback((l: Locale) => {
    if (SUPPORTED_LOCALES.includes(l)) setLocaleState(l)
  }, [])

  // 翻译函数：按当前语言查找
  const t = useCallback(
    (key: string) => {
      const dict = messages[locale]
      return dict[key] ?? key
    },
    [locale],
  )

  const value = useMemo(() => ({ locale, setLocale, t }), [locale, setLocale, t])

  return <I18nContext.Provider value={value}>{children}</I18nContext.Provider>
}

// 注意：Hook 已拆分至独立文件以满足 react-refresh 规则
