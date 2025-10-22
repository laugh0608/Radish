// 导航栏组件：负责全局站点导航、Logo 与移动端菜单
import { useState } from 'react'
import logo from '../assets/react.svg'
import './NavBar.css'

// 导航项：可根据需要扩展为路由跳转
const navItems = [
  { label: '\u6587\u6863', href: '#docs' },
  { label: '\u7279\u6027', href: '#features' },
  { label: '\u793e\u533a', href: '#community' },
]

const NavBar = () => {
  // 移动端菜单打开状态
  const [isMenuOpen, setIsMenuOpen] = useState(false)

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
            <a key={item.label} href={item.href}>
              {item.label}
            </a>
          ))}
        </nav>
        {/* 右侧操作按钮（示例：注册 / 登录） */}
        <div className="navbar__actions">
          <button className="navbar__button navbar__button--outline" type="button">
            {'\u6ce8\u518c'}
          </button>
          <button className="navbar__button navbar__button--solid" type="button">
            {'\u767b\u5f55'}
          </button>
        </div>
        {/* 移动端菜单切换按钮（汉堡） */}
        <button
          className="navbar__toggle"
          type="button"
          aria-expanded={isMenuOpen}
          aria-label="\u5207\u6362\u5bfc\u822a\u83dc\u5355"
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
            <a key={item.label} href={item.href}>
              {item.label}
            </a>
          ))}
        </nav>
        <div className="navbar__mobile-actions">
          <button className="navbar__button navbar__button--outline" type="button" onClick={closeMenu}>
            {'\u6ce8\u518c'}
          </button>
          <button className="navbar__button navbar__button--solid" type="button" onClick={closeMenu}>
            {'\u767b\u5f55'}
          </button>
        </div>
      </div>
    </header>
  )
}

export default NavBar
