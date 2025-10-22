// 应用入口：挂载 React 根节点并启用严格模式（开发期额外检查）
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
// 全局样式（重置与设计令牌），仅在入口处引入一次
import './index.css'
// 根组件
import App from './App.tsx'

// 创建并渲染应用到 index.html 中 id 为 root 的容器
createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
)
