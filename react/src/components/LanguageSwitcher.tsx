// 语言切换器：在中英之间切换
import { useI18n } from '../lib/i18n/useI18n'
import type { Locale } from '../lib/i18n/messages'

const options: { value: Locale; labelKey: string }[] = [
  { value: 'zh-CN', labelKey: 'lang.zhCN' },
  { value: 'en', labelKey: 'lang.en' },
]

const LanguageSwitcher = () => {
  const { locale, setLocale, t } = useI18n()

  // 选择后即时切换语言
  const onChange = (e: React.ChangeEvent<HTMLSelectElement>) => {
    setLocale(e.target.value as Locale)
  }

  return (
    <label style={{ display: 'inline-flex', alignItems: 'center', gap: 6 }}>
      {/* 无样式内联实现，避免修改现有 CSS */}
      <span className="visually-hidden" aria-hidden={false}>
        {t('aria.langSwitcher')}
      </span>
      <select aria-label={t('aria.langSwitcher')} value={locale} onChange={onChange}>
        {options.map((opt) => (
          <option key={opt.value} value={opt.value}>
            {t(opt.labelKey)}
          </option>
        ))}
      </select>
    </label>
  )
}

export default LanguageSwitcher
