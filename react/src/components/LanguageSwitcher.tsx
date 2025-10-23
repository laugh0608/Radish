// 语言切换器（图标按钮 + 下拉菜单）
import { useEffect, useRef, useState } from 'react'
import { useI18n } from '../lib/i18n/useI18n'
import type { Locale } from '../lib/i18n/messages'
import './LanguageSwitcher.css'

type LangOption = { value: Locale; labelKey: string }

const OPTIONS: LangOption[] = [
  { value: 'zh-CN', labelKey: 'lang.zhCN' },
  { value: 'en', labelKey: 'lang.en' },
]

const localeBadge: Record<Locale, string> = {
  'zh-CN': '中',
  en: 'EN',
}

const LanguageSwitcher = () => {
  const { locale, setLocale, t } = useI18n()
  const [open, setOpen] = useState(false)
  const ref = useRef<HTMLDivElement | null>(null)

  // 关闭菜单：点击外部或 Esc
  useEffect(() => {
    const onDocClick = (e: MouseEvent | TouchEvent) => {
      if (!ref.current) return
      if (e.target instanceof Node && ref.current.contains(e.target)) return
      setOpen(false)
    }
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') setOpen(false)
    }
    document.addEventListener('mousedown', onDocClick)
    document.addEventListener('touchstart', onDocClick)
    document.addEventListener('keydown', onKey)
    return () => {
      document.removeEventListener('mousedown', onDocClick)
      document.removeEventListener('touchstart', onDocClick)
      document.removeEventListener('keydown', onKey)
    }
  }, [])

  const onSelect = (l: Locale) => {
    setLocale(l)
    setOpen(false)
  }

  return (
    <div ref={ref} className="lang-switcher">
      <button
        type="button"
        className="lang-switcher__button"
        aria-haspopup="menu"
        aria-expanded={open}
        aria-label={t('aria.langSwitcher')}
        onClick={() => setOpen((v) => !v)}
      >
        {/* 地球图标 */}
        <svg
          className="lang-switcher__icon"
          xmlns="http://www.w3.org/2000/svg"
          width="18"
          height="18"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          strokeWidth="2"
          strokeLinecap="round"
          strokeLinejoin="round"
          aria-hidden="true"
          focusable="false"
        >
          <circle cx="12" cy="12" r="10" />
          <path d="M2 12h20" />
          <path d="M12 2c2.5 2.8 4 6.3 4 10s-1.5 7.2-4 10c-2.5-2.8-4-6.3-4-10S9.5 4.8 12 2z" />
        </svg>
        <span className="lang-switcher__badge" aria-hidden>
          {localeBadge[locale]}
        </span>
      </button>

      {open && (
        <div role="menu" className="lang-switcher__menu">
          {OPTIONS.map((opt) => (
            <button
              key={opt.value}
              role="menuitemradio"
              aria-checked={locale === opt.value}
              className={`lang-switcher__menu-item ${locale === opt.value ? 'is-active' : ''}`}
              onClick={() => onSelect(opt.value)}
              type="button"
            >
              <span>{t(opt.labelKey)}</span>
              {locale === opt.value ? (
                <svg
                  xmlns="http://www.w3.org/2000/svg"
                  width="16"
                  height="16"
                  viewBox="0 0 24 24"
                  fill="none"
                  stroke="currentColor"
                  strokeWidth="3"
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  aria-hidden="true"
                >
                  <polyline points="20 6 9 17 4 12" />
                </svg>
              ) : null}
            </button>
          ))}
        </div>
      )}
    </div>
  )
}

export default LanguageSwitcher
