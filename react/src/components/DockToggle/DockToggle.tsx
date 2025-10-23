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
        <span className="dock-toggle__thumb" />
      </span>
    </button>
  )
}

export default DockToggle

