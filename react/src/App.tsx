// 应用根组件：组织全局导航与主要内容区域
import './App.css'
// 站点顶栏导航组件
import NavBar from './components/NavBar'
import ThreeColumnLayout from './components/ThreeColumnLayout'
import SidebarCard from './components/SidebarCard'
import SectionList from './components/SectionList'
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
            <SidebarCard
              title={t('layout.left.actions')}
              icon={
                <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                  <path d="M12 2v4M12 18v4M4.93 4.93l2.83 2.83M16.24 16.24l2.83 2.83M2 12h4M18 12h4M4.93 19.07l2.83-2.83M16.24 7.76l2.83-2.83" />
                  <circle cx="12" cy="12" r="3" />
                </svg>
              }
            >
              <div>
                <button className="btn-ghost" type="button">{t('actions.post')}</button>
                <button className="btn-ghost" type="button">{t('actions.drafts')}</button>
                <button className="btn-ghost" type="button">{t('actions.notifications')}</button>
              </div>
            </SidebarCard>
            <SidebarCard
              title={t('layout.left.sections')}
              icon={
                <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                  <rect x="3" y="3" width="7" height="7" rx="2" />
                  <rect x="14" y="3" width="7" height="7" rx="2" />
                  <rect x="14" y="14" width="7" height="7" rx="2" />
                  <rect x="3" y="14" width="7" height="7" rx="2" />
                </svg>
              }
            >
              <SectionList
                items={[
                  { label: t('sections.news'), count: 12 },
                  { label: t('sections.tech'), count: 8 },
                  { label: t('sections.share'), count: 5 },
                ]}
              />
            </SidebarCard>
          </div>
        }
        right={
          <div>
            <SidebarCard title={t('layout.right.illustration')} sticky>
              <img src={RadishAcg} alt="Radish illustration" />
            </SidebarCard>
            <SidebarCard title={t('layout.right.placeholderA')}>
              <p>{t('layout.right.placeholderA')} - Lorem ipsum dolor sit amet.</p>
            </SidebarCard>
            <SidebarCard title={t('layout.right.placeholderB')}>
              <p>{t('layout.right.placeholderB')} - Vivamus sagittis lacus vel augue.</p>
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
