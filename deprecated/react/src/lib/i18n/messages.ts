// React 侧已改为从 ABP 后端获取本地化资源。
// 本文件仅保留类型与空字典，作为离线兜底的占位。
export type Locale = 'en' | 'zh-Hans'
export const messages: Record<Locale, Record<string, string>> = { en: {}, 'zh-Hans': {} }
export const SUPPORTED_LOCALES: Locale[] = ['en', 'zh-Hans']
