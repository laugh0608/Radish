import './BottomBar.css'
import { useI18n } from '../../lib/i18n/useI18n'
import { useScrollDirection } from '../../lib/hooks/useScrollDirection'
import { useEffect, useRef, useState } from 'react'

type DockItem = {
  id: string
  labelKey: string
  icon: React.ReactNode
  onClick?: () => void
  // 可选等级：越大越容易被隐藏
  optionalLevel?: 1 | 2
}

const iconStyle = { width: '1em', height: '1em', display: 'block' }

const ArrowUpIcon = (
  <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" style={iconStyle}>
    <path d="M12 19V5" />
    <path d="M5 12l7-7 7 7" />
  </svg>
)

const HomeIcon = (
  <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" style={iconStyle}>
    <path d="M3 12l9-9 9 9" />
    <path d="M9 21V9h6v12" />
  </svg>
)

const SearchIcon = (
  <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" style={iconStyle}>
    <circle cx="11" cy="11" r="7" />
    <path d="M21 21l-4.3-4.3" />
  </svg>
)

const PlusIcon = (
  <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" style={iconStyle}>
    <path d="M12 5v14M5 12h14" />
  </svg>
)

const BellIcon = (
  <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" style={iconStyle}>
    <path d="M18 8a6 6 0 10-12 0c0 7-3 9-3 9h18s-3-2-3-9" />
    <path d="M13.73 21a2 2 0 01-3.46 0" />
  </svg>
)

const UserIcon = (
  <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" style={iconStyle}>
    <path d="M20 21v-2a4 4 0 00-4-4H8a4 4 0 00-4 4v2" />
    <circle cx="12" cy="7" r="4" />
  </svg>
)

const SettingsIcon = (
  <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" style={iconStyle}>
    <circle cx="12" cy="12" r="3" />
    <path d="M19.4 15a1.65 1.65 0 00.33 1.82l.06.06a2 2 0 11-2.83 2.83l-.06-.06a1.65 1.65 0 00-1.82-.33 1.65 1.65 0 00-1 1.51V21a2 2 0 11-4 0v-.09a1.65 1.65 0 00-1-1.51 1.65 1.65 0 00-1.82.33l-.06.06a2 2 0 11-2.83-2.83l.06-.06a1.65 1.65 0 00.33-1.82 1.65 1.65 0 00-1.51-1H3a2 2 0 110-4h.09a1.65 1.65 0 001.51-1 1.65 1.65 0 00-.33-1.82l-.06-.06a2 2 0 112.83-2.83l.06.06a1.65 1.65 0 001.82.33H9a1.65 1.65 0 001-1.51V3a2 2 0 114 0v.09a1.65 1.65 0 001 1.51 1.65 1.65 0 001.82-.33l.06-.06a2 2 0 112.83 2.83l-.06.06a1.65 1.65 0 00-.33 1.82V9c0 .66.39 1.26 1 1.51H21a2 2 0 110 4h-.09a1.65 1.65 0 00-1.51 1z" />
  </svg>
)

type MenuItem = { id: string; labelKey: string; onClick?: () => void }

const BottomBar = ({ enabled = true }: { enabled?: boolean }) => {
  const { t } = useI18n()
  const { visible } = useScrollDirection({ threshold: 4, minScrollTop: 80, idleMs: 260 })
  const ref = useRef<HTMLDivElement | null>(null)
  const [menuFor, setMenuFor] = useState<string | null>(null)
  const btnRefs = useRef<Record<string, HTMLButtonElement | null>>({})
  const listRef = useRef<HTMLUListElement | null>(null)
  const raf = useRef<number | null>(null)
  const hoverCapable = typeof window !== 'undefined' ? window.matchMedia('(hover: hover)').matches : false

  // 根据 Dock 实际高度为页面添加自适应底部内边距，避免内容被遮挡
  useEffect(() => {
    const root = document.documentElement
    const update = () => {
      if (!enabled || !ref.current) {
        root.style.setProperty('--dock-inset-bottom', '0px')
        return
      }
      const h = Math.ceil(ref.current.getBoundingClientRect().height + 12)
      root.style.setProperty('--dock-inset-bottom', visible ? `${h}px` : '0px')
    }
    update()
    const onResize = () => update()
    window.addEventListener('resize', onResize)
    const ro = (typeof ResizeObserver !== 'undefined') ? new ResizeObserver(update) : null
    if (ro && ref.current) ro.observe(ref.current)
    return () => {
      window.removeEventListener('resize', onResize)
      ro?.disconnect()
      root.style.removeProperty('--dock-inset-bottom')
    }
  }, [enabled, visible])

  // 关闭菜单：点击外部或按 Esc
  useEffect(() => {
    const onDoc = (e: MouseEvent) => {
      if (!ref.current) return
      if (e.target instanceof Node && ref.current.contains(e.target)) return
      setMenuFor(null)
    }
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') setMenuFor(null) }
    document.addEventListener('mousedown', onDoc)
    document.addEventListener('keydown', onKey)
    return () => {
      document.removeEventListener('mousedown', onDoc)
      document.removeEventListener('keydown', onKey)
    }
  }, [])

  if (!enabled) return null

  const scrollToTop = () => {
    window.scrollTo({ top: 0, behavior: 'smooth' })
  }

  const items: DockItem[] = [
    { id: 'toTop', labelKey: 'dock.toTop', icon: ArrowUpIcon, onClick: scrollToTop },
    { id: 'home', labelKey: 'dock.home', icon: HomeIcon },
    { id: 'search', labelKey: 'dock.search', icon: SearchIcon },
    { id: 'new', labelKey: 'dock.new', icon: PlusIcon },
    { id: 'bell', labelKey: 'dock.notifications', icon: BellIcon, optionalLevel: 1 },
    { id: 'user', labelKey: 'dock.profile', icon: UserIcon, optionalLevel: 1 },
    { id: 'settings', labelKey: 'dock.settings', icon: SettingsIcon, optionalLevel: 2 },
  ]

  const contextMenus: Record<string, MenuItem[]> = {
    home: [
      { id: 'open', labelKey: 'menu.open' },
      { id: 'pin', labelKey: 'menu.pin' },
    ],
    bell: [
      { id: 'open', labelKey: 'menu.open' },
      { id: 'clear', labelKey: 'menu.clear' },
      { id: 'mute', labelKey: 'menu.mute' },
    ],
    settings: [
      { id: 'open', labelKey: 'menu.open' },
    ],
  }

  return (
    <div ref={ref} className={`bottom-dock ${visible ? 'bottom-dock--visible' : 'bottom-dock--hidden'}`} role="navigation" aria-label={t('aria.dock')}>
      <ul
        ref={listRef}
        className="bottom-dock__list"
        onMouseMove={(e) => {
          if (!hoverCapable) return
          const x = e.clientX
          if (raf.current) cancelAnimationFrame(raf.current)
          raf.current = requestAnimationFrame(() => {
            const radius = 120
            const max = 1.65
            const sigma = radius / 2.5
            const twoSigmaSq = 2 * sigma * sigma
            const entries = Object.entries(btnRefs.current)
            for (const [, el] of entries) {
              if (!el) continue
              const rect = el.getBoundingClientRect()
              const cx = rect.left + rect.width / 2
              const d = Math.abs(cx - x)
              const influence = Math.exp(-(d * d) / twoSigmaSq)
              const scale = 1 + (max - 1) * influence
              el.style.setProperty('--dock-scale', scale.toFixed(3))
            }
          })
        }}
        onMouseLeave={() => {
          const entries = Object.entries(btnRefs.current)
          for (const [, el] of entries) el?.style.setProperty('--dock-scale', '1')
        }}
      >
        {items.map((it) => {
          const menu = contextMenus[it.id]
          const isOpen = menuFor === it.id
          const onContextMenu = (e: React.MouseEvent) => {
            if (!menu) return
            e.preventDefault()
            setMenuFor(it.id)
          }
          let longPressTimer: number | null = null
          const onTouchStart = () => {
            if (!menu) return
            longPressTimer = window.setTimeout(() => setMenuFor(it.id), 560)
          }
          const clearLong = () => { if (longPressTimer) { window.clearTimeout(longPressTimer); longPressTimer = null } }
          return (
            <li
              key={it.id}
              className={`bottom-dock__item ${it.optionalLevel ? `optional-${it.optionalLevel}` : ''}`.trim()}
            >
              <button
                aria-label={t(it.labelKey)}
                className="bottom-dock__btn"
                type="button"
                onClick={it.onClick}
                onContextMenu={onContextMenu}
                onTouchStart={onTouchStart}
                onTouchEnd={clearLong}
                onTouchMove={clearLong}
                data-tooltip={t(it.labelKey)}
                ref={(el) => { btnRefs.current[it.id] = el }}
              >
                {it.icon}
                {it.id === 'bell' ? <span className="bottom-dock__badge" aria-hidden>3</span> : null}
              </button>
              {/* Tooltip */}
              <div className="bottom-dock__tooltip" aria-hidden>{t(it.labelKey)}</div>
              {/* Context Menu */}
              {menu && isOpen ? (
                <div className="bottom-dock__menu" role="menu">
                  {menu.map((m) => (
                    <button key={m.id} role="menuitem" className="bottom-dock__menu-item" type="button" onClick={() => { setMenuFor(null); m.onClick?.() }}>
                      {t(m.labelKey)}
                    </button>
                  ))}
                </div>
              ) : null}
            </li>
          )
        })}
      </ul>
    </div>
  )
}

export default BottomBar
