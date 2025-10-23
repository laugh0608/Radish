import './SidebarCard.css'

type Props = {
  title?: React.ReactNode
  icon?: React.ReactNode
  action?: React.ReactNode
  children?: React.ReactNode
}

const SidebarCard = ({ title, icon, action, children }: Props) => {
  return (
    <section className="sidebar-card">
      {(title || action) && (
        <header className="sidebar-card__header">
          <div className="sidebar-card__title">
            {icon ? <span className="sidebar-card__icon" aria-hidden>{icon}</span> : null}
            {title}
          </div>
          {action ? <div className="sidebar-card__action">{action}</div> : null}
        </header>
      )}
      <div className="sidebar-card__body">{children}</div>
    </section>
  )
}

export default SidebarCard
