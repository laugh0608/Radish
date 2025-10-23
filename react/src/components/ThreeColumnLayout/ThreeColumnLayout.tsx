import './ThreeColumnLayout.css'

type Props = {
  left?: React.ReactNode
  right?: React.ReactNode
  children: React.ReactNode
}

const ThreeColumnLayout = ({ left, right, children }: Props) => {
  return (
    <div className="three-col">
      <aside className="three-col__left" aria-label="Left sidebar">
        {left}
      </aside>
      <section className="three-col__main" aria-label="Main content">
        {children}
      </section>
      <aside className="three-col__right" aria-label="Right sidebar">
        {right}
      </aside>
    </div>
  )
}

export default ThreeColumnLayout

