import './SidebarCard.css'

type Props = {
  children: React.ReactNode
}

const SidebarCard = ({ children }: Props) => {
  return <div className="sidebar-card">{children}</div>
}

export default SidebarCard

