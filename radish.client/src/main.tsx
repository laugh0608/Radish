import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import './i18n'
// 代码高亮样式（用于 Markdown 渲染器）
// 注意：需要先安装 highlight.js: npm install --workspace=radish.client highlight.js
import 'highlight.js/styles/github-dark.css'
import App from './App.tsx'
import { Shell } from './desktop/Shell.tsx'

// 检查是否是 OIDC 回调页面
const isBrowser = typeof window !== 'undefined';
const isOidcCallback = isBrowser && window.location.pathname === '/oidc/callback';

// 通过 URL 参数控制显示不同的页面
// - /?demo - 查看原有的 OIDC Demo 页面（保留用于测试）
// - / - 默认显示 WebOS Desktop Shell（包含所有应用）
const params = new URLSearchParams(window.location.search);
const isDemo = params.has('demo');

let Page = Shell; // 默认显示 Shell
if (isOidcCallback || isDemo) {
  Page = App;
}

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <Page />
  </StrictMode>,
)
