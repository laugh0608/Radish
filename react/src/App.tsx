// 应用根组件：组织全局导航与主要内容区域
import './App.css'
// 站点顶栏导航组件
import NavBar from './components/NavBar'
// 引入 i18n Hook 以读取文案
import { useI18n } from './lib/i18n/useI18n'

function App() {
  // 通过 t(key) 读取多语言文案
  const { t } = useI18n()
  return (
    <>
      {/* 顶部导航栏（全局一致） */}
      <NavBar />
      {/* 主要内容区：按页面/功能模块填充 */}
      <main className="app-content">
        <h1>{t('app.welcome')}</h1>
        <p>{t('app.getStarted')}</p>
      </main>
    </>
  )
}

export default App
