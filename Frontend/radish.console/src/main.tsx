import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { antdLocales, ThemeProvider, App as AntApp, AntdFeedbackBridge } from '@radish/ui'
import { useTranslation } from 'react-i18next'
import { getApiBaseUrl } from './config/env'
import { applySiteBranding } from './services/siteBranding'
import { rememberClientBackTo } from './utils/clientNavigation'
import './index.css'
import App from './App.tsx'
import './i18n'
import { LanguageProvider } from './i18n/LanguageProvider'
import { normalizeLanguage } from './locales/language'

rememberClientBackTo(window.location.search)
void applySiteBranding(getApiBaseUrl())

export function ConsoleRoot() {
  const { i18n } = useTranslation()
  const language = normalizeLanguage(i18n.resolvedLanguage ?? i18n.language) ?? 'zh'

  return (
    <LanguageProvider>
      <ThemeProvider locale={antdLocales[language]}>
        <AntApp>
          <AntdFeedbackBridge>
            <App />
          </AntdFeedbackBridge>
        </AntApp>
      </ThemeProvider>
    </LanguageProvider>
  )
}

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <ConsoleRoot />
  </StrictMode>,
)
