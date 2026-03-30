import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { ThemeProvider, App as AntApp } from '@radish/ui'
import { getApiBaseUrl } from './config/env'
import { applySiteBranding } from './services/siteBranding'
import './index.css'
import App from './App.tsx'

void applySiteBranding(getApiBaseUrl())

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <ThemeProvider>
      <AntApp>
        <App />
      </AntApp>
    </ThemeProvider>
  </StrictMode>,
)
