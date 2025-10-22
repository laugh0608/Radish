import logo from '../assets/react.svg'
import './NavBar.css'

const NavBar = () => {
  return (
    <header className="navbar">
      <div className="navbar__logo">
        <img src={logo} alt="Radish logo" />
      </div>
      <div className="navbar__actions">
        <button className="navbar__button navbar__button--outline">注册</button>
        <button className="navbar__button navbar__button--solid">登录</button>
      </div>
    </header>
  )
}

export default NavBar
