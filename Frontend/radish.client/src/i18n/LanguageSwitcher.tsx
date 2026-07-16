import { Icon } from '@radish/ui/icon';
import { useTranslation } from 'react-i18next';
import { normalizeLanguage, type SupportedLanguage } from '@/locales/language';
import styles from './LanguageSwitcher.module.css';

export function LanguageSwitcher() {
  const { t, i18n } = useTranslation();
  const language = normalizeLanguage(i18n.resolvedLanguage ?? i18n.language) ?? 'zh';
  const nextLanguage: SupportedLanguage = language === 'zh' ? 'en' : 'zh';

  return (
    <button
      type="button"
      className={styles.trigger}
      aria-label={t('lang.switch')}
      title={t('lang.switchTo', { language: t(`lang.${nextLanguage}`) })}
      onClick={() => void i18n.changeLanguage(nextLanguage)}
    >
      <Icon icon="mdi:translate" size={18} />
      <span>{t(`lang.${language}`)}</span>
    </button>
  );
}
