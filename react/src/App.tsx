import './App.css'
import NavBar from './components/NavBar'

function App() {
  return (
    <>
      <NavBar />
      <main className="app-content">
        <h1>{'\u6b22\u8fce\u6765\u5230 Radish'}</h1>
        <p>{'\u5f00\u59cb\u6784\u5efa\u4f60\u7684 React \u5e94\u7528\u3002'}</p>
      </main>
    </>
  )
}

export default App
