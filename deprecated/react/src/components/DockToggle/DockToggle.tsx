import './DockToggle.css'
import { useI18n } from '../../lib/i18n/useI18n'

type Props = {
  enabled: boolean
  onToggle?: () => void
}

const DockToggle = ({ enabled, onToggle }: Props) => {
  const { t } = useI18n()
  return (
    <button
      type="button"
      className={`dock-toggle ${enabled ? 'is-on' : 'is-off'}`}
      aria-pressed={enabled}
      aria-label={t('aria.toggleDock')}
      onClick={onToggle}
    >
      <span className="dock-toggle__track">
        <span className="dock-toggle__thumb" aria-hidden>
          {/* Dock glyph inside thumb to match other icon buttons */}
          <svg
            className="dock-toggle__icon"
            xmlns="http://www.w3.org/2000/svg"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="2"
            strokeLinecap="round"
            strokeLinejoin="round"
          >
            {/* 底部托盘 */}
            <path d="M4 17h16" />
            {/* 上方 5 个图标方块 */}
            <rect x="5" y="8.5" width="2.8" height="2.8" rx="0.6" />
            <rect x="9" y="8.5" width="2.8" height="2.8" rx="0.6" />
            <rect x="13" y="8.5" width="2.8" height="2.8" rx="0.6" />
            <rect x="17" y="8.5" width="2.8" height="2.8" rx="0.6" />
          </svg>
        </span>
      </span>
    </button>
  )
}

export default DockToggle
