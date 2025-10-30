// I18n 上下文定义：仅导出类型与 Context（无组件）
import { createContext } from 'react'

// 与后端 ABP 资源对齐：使用 zh-Hans / en
export type Locale = 'en' | 'zh-Hans'

export type I18nCtx = {
  // 当前语言标识
  locale: Locale
  // 设置语言
  setLocale: (l: Locale) => void
  // 翻译函数
  t: (key: string) => string
}

export const I18nContext = createContext<I18nCtx | null>(null)
