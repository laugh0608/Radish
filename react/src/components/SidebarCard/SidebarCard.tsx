import './SidebarCard.css'
import type { CSSProperties } from 'react'
import { forwardRef } from 'react'

type Props = {
  title?: React.ReactNode
  icon?: React.ReactNode
  action?: React.ReactNode
  children?: React.ReactNode
  sticky?: boolean
  stickyTop?: number
}

const SidebarCard = forwardRef<HTMLElement, Props>(
  ({ title, icon, action, children, sticky, stickyTop }, ref) => {
    const style: CSSProperties | undefined =
      stickyTop !== undefined ? (({ ['--sticky-top' as any]: `${stickyTop}px` } as unknown) as CSSProperties) : undefined
    return (
      <section ref={ref as any} className={`sidebar-card ${sticky ? 'sticky' : ''}`} style={style}>
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
  },
)

export default SidebarCard
