import { Icon } from '@radish/ui/icon';
import { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { redirectToLogin } from '@/services/auth';
import { buildPublicShopPath } from '@/public/shopRouteState';
import { useAuthStore } from '@/stores/authStore';
import { useThemeStore } from '@/stores/themeStore';
import { selectTheme } from './themeEntitlements';
import { themeOptions, type ThemeDefinition } from './theme';
import styles from './ThemeSwitcher.module.css';

function buildCurrentReturnPath(): string {
  return `${window.location.pathname}${window.location.search}${window.location.hash}`;
}

function navigateToThemeShop(): void {
  const path = buildPublicShopPath({
    kind: 'products',
    keyword: '主题',
    page: 1,
  });
  window.history.pushState({}, '', path);
  window.dispatchEvent(new PopStateEvent('popstate', { state: window.history.state }));
}

export function ThemeSwitcher() {
  const { t } = useTranslation();
  const containerRef = useRef<HTMLDivElement>(null);
  const [open, setOpen] = useState(false);
  const theme = useThemeStore(state => state.theme);
  const ownedEntitlements = useThemeStore(state => state.ownedEntitlements);
  const isSyncing = useThemeStore(state => state.isSyncingEntitlements);
  const error = useThemeStore(state => state.entitlementError);
  const isAuthenticated = useAuthStore(state => state.isAuthenticated);
  const currentTheme = themeOptions.find(item => item.id === theme) ?? themeOptions[0];

  useEffect(() => {
    if (!open) {
      return;
    }

    const handlePointerDown = (event: PointerEvent) => {
      if (!containerRef.current?.contains(event.target as Node)) {
        setOpen(false);
      }
    };
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        setOpen(false);
      }
    };

    document.addEventListener('pointerdown', handlePointerDown);
    document.addEventListener('keydown', handleKeyDown);
    return () => {
      document.removeEventListener('pointerdown', handlePointerDown);
      document.removeEventListener('keydown', handleKeyDown);
    };
  }, [open]);

  const getEntitlement = (definition: ThemeDefinition) =>
    definition.access === 'entitlement'
      ? ownedEntitlements.find(item => item.themeId === definition.id)
      : undefined;

  const handleSelect = async (definition: ThemeDefinition) => {
    if (definition.access === 'entitlement') {
      if (!isAuthenticated) {
        redirectToLogin({ returnPath: buildCurrentReturnPath() });
        return;
      }

      if (!getEntitlement(definition)) {
        navigateToThemeShop();
        return;
      }
    }

    try {
      await selectTheme(definition.id);
      setOpen(false);
    } catch {
      // 结构化错误由主题状态展示在当前弹层。
    }
  };

  return (
    <div className={styles.container} ref={containerRef}>
      <button
        type="button"
        className={styles.trigger}
        aria-haspopup="dialog"
        aria-expanded={open}
        aria-label={t('theme.switch')}
        title={`${t('theme.switch')}：${t(currentTheme.labelKey)}`}
        onClick={() => setOpen(value => !value)}
      >
        <Icon icon={currentTheme.colorScheme === 'dark' ? 'mdi:weather-night' : 'mdi:palette-outline'} size={18} />
        <span>{t(currentTheme.labelKey)}</span>
      </button>

      {open && (
        <section className={styles.panel} role="dialog" aria-label={t('theme.menuTitle')}>
          <div className={styles.heading}>
            <div>
              <p className={styles.kicker}>{t('theme.menuKicker')}</p>
              <h2>{t('theme.menuTitle')}</h2>
            </div>
            <span className={styles.currentBadge}>{t(currentTheme.labelKey)}</span>
          </div>

          <div className={styles.options}>
            {themeOptions.map(definition => {
              const entitlement = getEntitlement(definition);
              const active = definition.id === theme;
              const locked = definition.access === 'entitlement' && !entitlement;
              const status = definition.access === 'builtin'
                ? t('theme.builtin')
                : entitlement
                  ? t('theme.owned')
                  : isAuthenticated
                    ? t('theme.openShop')
                    : t('theme.signInRequired');

              return (
                <button
                  key={definition.id}
                  type="button"
                  className={`${styles.option} ${active ? styles.optionActive : ''}`}
                  aria-pressed={active}
                  disabled={isSyncing}
                  onClick={() => void handleSelect(definition)}
                >
                  <span className={styles.swatch} data-theme-preview={definition.id} aria-hidden="true" />
                  <span className={styles.optionCopy}>
                    <strong>{t(definition.labelKey)}</strong>
                    <small>{active ? t('theme.active') : status}</small>
                  </span>
                  <Icon
                    icon={active ? 'mdi:check-circle' : locked ? 'mdi:lock-outline' : 'mdi:chevron-right'}
                    size={18}
                  />
                </button>
              );
            })}
          </div>

          {error && <p className={styles.error} role="alert">{error}</p>}
          {isSyncing && <p className={styles.syncing} aria-live="polite">{t('theme.syncing')}</p>}
        </section>
      )}
    </div>
  );
}
