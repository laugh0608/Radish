// 应用入口：挂载 React 根节点并启用严格模式（开发期额外检查）
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
// 全局样式（重置与设计令牌），仅在入口处引入一次
import './index.css'
// 根组件
import App from './App.tsx'
// 本地化提供者（包裹应用以启用翻译能力）
import { I18nProvider } from './lib/i18n/I18nProvider'
import { ThemeProvider } from './lib/theme/ThemeProvider'

// 创建并渲染应用到 index.html 中 id 为 root 的容器
createRoot(document.getElementById('root')!).render(
  <StrictMode>
    {/* 全局主题与本地化上下文 */}
    <ThemeProvider>
      <I18nProvider>
        <App />
      </I18nProvider>
    </ThemeProvider>
  </StrictMode>,
)
