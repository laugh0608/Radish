// 基于 ABP 的应用配置/本地化获取封装
// 仅实现所需的最小子集，供 React 端 I18nProvider 使用

export type AbpCulture = {
  cultureName: string
  uiCultureName?: string
}

export type AbpLocalizationPayload = {
  currentCulture: AbpCulture
  values: Record<string, Record<string, string>> // { ResourceName: { Key: Value } }
}

export type AbpAppConfig = {
  localization?: AbpLocalizationPayload
}

const DEFAULT_API_BASE = 'http://localhost:44342'

function getApiBase() {
  // Vite 注入：优先使用环境变量
  // eslint-disable-next-line no-undef
  const v = (import.meta as any).env?.VITE_API_BASE_URL as string | undefined
  return v && v.length > 0 ? v : DEFAULT_API_BASE
}

export async function fetchAbpLocalization(locale: string): Promise<AbpLocalizationPayload> {
  const base = getApiBase()
  const url = `${base}/api/abp/application-configuration?includeLocalizationResources=true`
  const res = await fetch(url, {
    headers: {
      'Accept': 'application/json',
      'Accept-Language': locale,
    },
    credentials: 'include', // 兼容同域 Cookie 场景
  })
  if (!res.ok) throw new Error(`fetch application-configuration failed: ${res.status}`)
  const data = (await res.json()) as AbpAppConfig
  if (!data.localization) throw new Error('no localization in application-configuration response')
  return data.localization
}
