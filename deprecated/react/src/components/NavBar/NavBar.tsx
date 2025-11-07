// 导航栏组件：负责全局站点导航、Logo 与移动端菜单
import { useState } from 'react'
import type { FormEvent } from 'react'
import logo from '../../assets/react.svg'
import './NavBar.css'
// i18n：翻译与语言切换
import { useI18n } from '../../lib/i18n/useI18n'
import LanguageSwitcher from '../LanguageSwitcher'
import ThemeToggle from '../ThemeToggle'
import DockToggle from '../DockToggle'

type Props = {
  dockEnabled?: boolean
  onToggleDock?: () => void
}

// 顶部中部改为搜索框，原导航项移除

const NavBar = ({ dockEnabled = true, onToggleDock }: Props) => {
  // 移动端菜单打开状态
  const [isMenuOpen, setIsMenuOpen] = useState(false)
  // 读取翻译函数
  const { t } = useI18n()
  const [query, setQuery] = useState('')

  const onSearchSubmit = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault()
    // 暂无真实搜索后端，这里仅演示：可替换为路由跳转或回调
    // eslint-disable-next-line no-console
    console.log('search:', query)
  }

  // 切换移动端菜单开合
  const toggleMenu = () => {
    setIsMenuOpen((prev) => !prev)
  }

  // 关闭移动端菜单（用于点击菜单项或操作按钮后）
  const closeMenu = () => {
    setIsMenuOpen(false)
  }

  return (
    // 顶部固定导航条，含 Logo、主导航链接、操作按钮与移动端切换
    <header className={`navbar ${isMenuOpen ? 'navbar--expanded' : ''}`}>
      <div className="navbar__inner">
        {/* 品牌区（Logo + 品牌名） */}
        <div className="navbar__logo">
          <img src={logo} alt="Radish logo" />
          <span className="navbar__brand">Radish</span>
        </div>
        {/* 桌面端搜索框（居中） */}
        <form className="navbar__search" role="search" onSubmit={onSearchSubmit}>
          <input
            type="search"
            className="navbar__search-input"
            placeholder={t('search.placeholder')}
            value={query}
            onChange={(e) => setQuery(e.target.value)}
          />
        </form>
        {/* 右侧操作按钮（示例：注册 / 登录） */}
        <div className="navbar__actions">
          {/* 桌面端把两个按钮放在注册/登录左侧 */}
          <ThemeToggle />
          <LanguageSwitcher />
          <DockToggle enabled={dockEnabled} onToggle={onToggleDock} />
          <button className="navbar__button navbar__button--outline" type="button">
            {t('actions.signUp')}
          </button>
          <button className="navbar__button navbar__button--solid" type="button">
            {t('actions.signIn')}
          </button>
        </div>
        {/* 移动端菜单切换按钮（汉堡） */}
        <button
          className="navbar__toggle"
          type="button"
          aria-expanded={isMenuOpen}
          aria-label={t('aria.toggleNav')}
          onClick={toggleMenu}
        >
          <span />
          <span />
          <span />
        </button>
      </div>
      {/* 移动端抽屉面板：包含搜索与操作按钮 */}
      <div className="navbar__mobile-panel">
        <div className="navbar__mobile-search">
          <form role="search" onSubmit={onSearchSubmit}>
            <input
              type="search"
              className="navbar__search-input navbar__search-input--mobile"
              placeholder={t('search.placeholder')}
              value={query}
              onChange={(e) => setQuery(e.target.value)}
            />
          </form>
        </div>
        <div className="navbar__mobile-actions">
          <button className="navbar__button navbar__button--outline" type="button" onClick={closeMenu}>
            {t('actions.signUp')}
          </button>
          <button className="navbar__button navbar__button--solid" type="button" onClick={closeMenu}>
            {t('actions.signIn')}
          </button>
          {/* 移动端开关区：横向排布 */}
          <div className="navbar__mobile-controls">
            <ThemeToggle />
            <LanguageSwitcher />
            <DockToggle enabled={dockEnabled} onToggle={() => { onToggleDock?.(); closeMenu(); }} />
          </div>
        </div>
      </div>
    </header>
  )
}

export default NavBar
