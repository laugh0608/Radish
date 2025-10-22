import { useState } from 'react'
import logo from '../assets/react.svg'
import './NavBar.css'

const navItems = [
  { label: '\u6587\u6863', href: '#docs' },
  { label: '\u7279\u6027', href: '#features' },
  { label: '\u793e\u533a', href: '#community' },
]

const NavBar = () => {
  const [isMenuOpen, setIsMenuOpen] = useState(false)

  const toggleMenu = () => {
    setIsMenuOpen((prev) => !prev)
  }

  const closeMenu = () => {
    setIsMenuOpen(false)
  }

  return (
    <header className={`navbar ${isMenuOpen ? 'navbar--expanded' : ''}`}>
      <div className="navbar__inner">
        <div className="navbar__logo">
          <img src={logo} alt="Radish logo" />
          <span className="navbar__brand">Radish</span>
        </div>
        <nav className="navbar__links">
          {navItems.map((item) => (
            <a key={item.label} href={item.href}>
              {item.label}
            </a>
          ))}
        </nav>
        <div className="navbar__actions">
          <button className="navbar__button navbar__button--outline" type="button">
            {'\u6ce8\u518c'}
          </button>
          <button className="navbar__button navbar__button--solid" type="button">
            {'\u767b\u5f55'}
          </button>
        </div>
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
