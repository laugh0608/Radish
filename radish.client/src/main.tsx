import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import './i18n'
import App from './App.tsx'
import { Shell } from './desktop/Shell.tsx'
import { useUserStore } from './stores/userStore'

// 设置模拟用户数据（实际应该从 OIDC 登录后获取）
const userStore = useUserStore.getState();
userStore.setUser({
  userId: 1,
  userName: 'test',
  tenantId: 1,
  roles: ['User', 'Admin']
});

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
