import { TranslationOutlined } from '@radish/ui';
import { useTranslation } from 'react-i18next';
import { normalizeLanguage, type SupportedLanguage } from '@/locales/language';
import './LanguageSwitcher.css';

export function LanguageSwitcher() {
  const { t, i18n } = useTranslation();
  const language = normalizeLanguage(i18n.resolvedLanguage ?? i18n.language) ?? 'zh';
  const nextLanguage: SupportedLanguage = language === 'zh' ? 'en' : 'zh';

  return (
    <button
      type="button"
      className="console-language-switcher"
      aria-label={t('lang.switch')}
      title={`${t('lang.switch')}：${t(`lang.${nextLanguage}`)}`}
      onClick={() => void i18n.changeLanguage(nextLanguage)}
    >
      <TranslationOutlined />
      <span>{t(`lang.${language}`)}</span>
    </button>
  );
}
