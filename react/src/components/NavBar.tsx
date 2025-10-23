// 导航栏组件：负责全局站点导航、Logo 与移动端菜单
import { useState } from 'react'
import logo from '../assets/react.svg'
import './NavBar.css'
// i18n：翻译与语言切换
import { useI18n } from '../lib/i18n/useI18n'
import LanguageSwitcher from './LanguageSwitcher'
import ThemeToggle from './ThemeToggle'

// 导航项：仅保存 key，展示时通过 t(key) 翻译
const navItems = [
  { key: 'nav.docs', href: '#docs' },
  { key: 'nav.features', href: '#features' },
  { key: 'nav.community', href: '#community' },
]

const NavBar = () => {
  // 移动端菜单打开状态
  const [isMenuOpen, setIsMenuOpen] = useState(false)
  // 读取翻译函数
  const { t } = useI18n()

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
        {/* 桌面端主导航链接 */}
        <nav className="navbar__links">
          {navItems.map((item) => (
            <a key={item.key} href={item.href}>
              {t(item.key)}
            </a>
          ))}
        </nav>
        {/* 右侧操作按钮（示例：注册 / 登录） */}
        <div className="navbar__actions">
          <button className="navbar__button navbar__button--outline" type="button">
            {t('actions.signUp')}
          </button>
          <button className="navbar__button navbar__button--solid" type="button">
            {t('actions.signIn')}
          </button>
          {/* 主题切换 */}
          <ThemeToggle />
          {/* 语言切换器 */}
          <LanguageSwitcher />
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
      {/* 移动端抽屉面板：包含导航链接与操作按钮 */}
      <div className="navbar__mobile-panel">
        <nav className="navbar__mobile-links" onClick={closeMenu}>
          {navItems.map((item) => (
            <a key={item.key} href={item.href}>
              {t(item.key)}
            </a>
          ))}
        </nav>
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
          </div>
        </div>
      </div>
    </header>
  )
}

export default NavBar
