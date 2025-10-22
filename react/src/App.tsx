// 应用根组件：组织全局导航与主要内容区域
import './App.css'
// 站点顶栏导航组件
import NavBar from './components/NavBar'

function App() {
  return (
    <>
      {/* 顶部导航栏（全局一致） */}
      <NavBar />
      {/* 主要内容区：按页面/功能模块填充 */}
      <main className="app-content">
        <h1>{'\u6b22\u8fce\u6765\u5230 Radish'}</h1>
        <p>{'\u5f00\u59cb\u6784\u5efa\u4f60\u7684 React \u5e94\u7528\u3002'}</p>
      </main>
    </>
  )
}

export default App
