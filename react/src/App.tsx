// 应用根组件：组织全局导航与主要内容区域
import './App.css'
// 站点顶栏导航组件
import NavBar from './components/NavBar'
import ThreeColumnLayout from './components/ThreeColumnLayout'
import SidebarCard from './components/SidebarCard'
import RadishAcg from './assets/RadishAcg.png'
// 引入 i18n Hook 以读取文案
import { useI18n } from './lib/i18n/useI18n'

function App() {
  // 通过 t(key) 读取多语言文案
  const { t } = useI18n()
  return (
    <>
      <NavBar />
      <ThreeColumnLayout
        left={
          <div>
            <SidebarCard>
              <p>左侧卡片（上）- 未来放操作按钮</p>
            </SidebarCard>
            <SidebarCard>
              <p>左侧卡片（下）- 未来放帖子分区列表</p>
            </SidebarCard>
          </div>
        }
        right={
          <div>
            <SidebarCard>
              <img src={RadishAcg} alt="Radish illustration" />
            </SidebarCard>
            <SidebarCard>
              <p>右侧卡片占位内容 A</p>
            </SidebarCard>
            <SidebarCard>
              <p>右侧卡片占位内容 B</p>
            </SidebarCard>
          </div>
        }
      >
        <main className="app-content">
          <h1>{t('app.welcome')}</h1>
          <p>{t('app.getStarted')}</p>
        </main>
      </ThreeColumnLayout>
    </>
  )
}

export default App
