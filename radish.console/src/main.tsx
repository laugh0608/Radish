import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { ThemeProvider, App as AntApp } from '@radish/ui'
import './index.css'
import App from './App.tsx'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <ThemeProvider>
      <AntApp>
        <App />
      </AntApp>
    </ThemeProvider>
  </StrictMode>,
)
