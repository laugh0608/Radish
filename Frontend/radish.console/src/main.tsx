import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { ThemeProvider, App as AntApp, AntdFeedbackBridge } from '@radish/ui'
import { getApiBaseUrl } from './config/env'
import { applySiteBranding } from './services/siteBranding'
import { rememberClientBackTo } from './utils/clientNavigation'
import './index.css'
import App from './App.tsx'

rememberClientBackTo(window.location.search)
void applySiteBranding(getApiBaseUrl())

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <ThemeProvider>
      <AntApp>
        <AntdFeedbackBridge>
          <App />
        </AntdFeedbackBridge>
      </AntApp>
    </ThemeProvider>
  </StrictMode>,
)
