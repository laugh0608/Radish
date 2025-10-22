// I18n 上下文定义：仅导出类型与 Context（无组件）
import { createContext } from 'react'
import type { Locale } from './messages'

export type I18nCtx = {
  // 当前语言标识
  locale: Locale
  // 设置语言
  setLocale: (l: Locale) => void
  // 翻译函数
  t: (key: string) => string
}

export const I18nContext = createContext<I18nCtx | null>(null)

