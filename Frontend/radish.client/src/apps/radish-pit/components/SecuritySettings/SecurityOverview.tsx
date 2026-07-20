import { useTranslation } from 'react-i18next';
import { DEFAULT_TIME_ZONE, getBrowserTimeZoneId } from '@/utils/dateTime';
import { formatCoinDateTime, formatCoinNumber } from '../../utils';
import type { SecurityStatus } from '../../types';
import styles from './SecurityOverview.module.css';

interface SecurityOverviewProps {
  status: SecurityStatus | null;
  loading: boolean;
  error: string | null;
  onRefresh: () => void;
  onNavigate: (tab: 'password' | 'log' | 'tips') => void;
}

/**
 * 安全概览组件
 */
export const SecurityOverview = ({ status, loading, error, onRefresh, onNavigate }: SecurityOverviewProps) => {
  const { t, i18n } = useTranslation();
  const language = i18n.resolvedLanguage ?? i18n.language;
  const displayTimeZone = getBrowserTimeZoneId(DEFAULT_TIME_ZONE);
  if (loading) {
    return (
      <div className={styles.container}>
        <div className={styles.loading}>
          <div className={styles.loadingSpinner}></div>
          <p>{t('pit.security.loading')}</p>
        </div>
      </div>
    );
  }

  if (error) {
    return (
      <div className={styles.container}>
        <div className={styles.error}>
          <div className={styles.errorIcon}>⚠️</div>
          <h3>{t('pit.common.loadFailed')}</h3>
          <p>{error}</p>
          <button className={styles.retryButton} onClick={onRefresh}>
            {t('pit.common.retry')}
          </button>
        </div>
      </div>
    );
  }

  const requiresPasscodeUpgrade = Boolean(status?.requiresPasscodeUpgrade);

  const getSecurityScore = (): number => {
    if (!status) return 0;

    let score = 0;
    if (status.hasPaymentPassword && !status.requiresPasscodeUpgrade) score += 40;
    if (status.failedAttempts === 0) score += 20;
    if (!status.isLocked) score += 20;
    if (status.lastPasswordChangeTime && !status.requiresPasscodeUpgrade) score += 20;

    return Math.min(100, score);
  };

  const getSecurityScoreColor = (score: number): string => {
    if (requiresPasscodeUpgrade) return 'critical';
    if (score >= 80) return 'high';
    if (score >= 60) return 'medium';
    if (score >= 40) return 'low';
    return 'critical';
  };

  const securityScore = getSecurityScore();
  const scoreColor = getSecurityScoreColor(securityScore);
  const failedAttempts = status?.failedAttempts || 0;

  return (
    <div className={styles.container}>
      {/* 安全评分 */}
      <div className={styles.securityScore}>
        <div className={styles.scoreHeader}>
          <h3 className={styles.scoreTitle}>
            <span className={styles.scoreIcon}>🛡️</span>
            {t('pit.security.score.title')}
          </h3>
          <button className={styles.refreshButton} onClick={onRefresh} title={t('pit.common.refresh')}>
            🔄
          </button>
        </div>

        <div className={styles.scoreContent}>
          <div className={styles.scoreCircle}>
            <div className={`${styles.scoreProgress} ${styles[scoreColor]}`}>
              <div className={styles.scoreValue}>{securityScore}</div>
              <div className={styles.scoreMax}>/ 100</div>
            </div>
          </div>

          <div className={styles.scoreDescription}>
            <div className={`${styles.scoreLevel} ${styles[scoreColor]}`}>
              {t(requiresPasscodeUpgrade ? 'pit.security.state.resetRequired' :
               securityScore >= 80 ? 'pit.security.score.secure' :
               securityScore >= 60 ? 'pit.security.score.good' :
               securityScore >= 40 ? 'pit.security.score.fair' : 'pit.security.score.improve')}
            </div>
            <p className={styles.scoreText}>
              {t(requiresPasscodeUpgrade ? 'pit.security.score.upgradeDescription' :
               securityScore >= 80 ? 'pit.security.score.secureDescription' :
               securityScore >= 60 ? 'pit.security.score.goodDescription' :
               securityScore >= 40 ? 'pit.security.score.fairDescription' :
               'pit.security.score.improveDescription')}
            </p>
          </div>
        </div>
      </div>

      {/* 安全状态卡片 */}
      <div className={styles.securityCards}>
        {/* 支付口令状态 */}
        <div className={`${styles.securityCard} ${
          requiresPasscodeUpgrade ? styles.danger : status?.hasPaymentPassword ? styles.secure : styles.warning
        }`}>
          <div className={styles.cardIcon}>
            {requiresPasscodeUpgrade ? '♻️' : status?.hasPaymentPassword ? '🔑' : '⚠️'}
          </div>
          <div className={styles.cardContent}>
            <div className={styles.cardTitle}>{t('pit.security.passcode.title')}</div>
            <div className={styles.cardStatus}>
              {t(requiresPasscodeUpgrade
                ? 'pit.security.passcode.legacy'
                : status?.hasPaymentPassword
                  ? 'pit.security.passcode.configured'
                  : 'pit.security.passcode.notConfigured')}
            </div>
            <div className={styles.cardDescription}>
              {t(requiresPasscodeUpgrade
                ? 'pit.security.passcode.legacyDescription'
                : status?.hasPaymentPassword
                  ? 'pit.security.passcode.configuredDescription'
                  : 'pit.security.passcode.notConfiguredDescription')}
            </div>
          </div>
          <div className={styles.cardAction}>
            <button className={styles.actionButton} onClick={() => onNavigate('password')}>
              {t(requiresPasscodeUpgrade
                ? 'pit.security.passcode.resetNow'
                : status?.hasPaymentPassword
                  ? 'pit.security.passcode.change'
                  : 'pit.security.passcode.set')}
            </button>
          </div>
        </div>

        {/* 账户锁定状态 */}
        <div className={`${styles.securityCard} ${
          status?.isLocked ? styles.danger : styles.secure
        }`}>
          <div className={styles.cardIcon}>
            {status?.isLocked ? '🔒' : '🔓'}
          </div>
          <div className={styles.cardContent}>
            <div className={styles.cardTitle}>{t('pit.security.account.title')}</div>
            <div className={styles.cardStatus}>
              {t(status?.isLocked ? 'pit.security.state.locked' : 'pit.security.account.normal')}
            </div>
            <div className={styles.cardDescription}>
              {status?.isLocked
                ? status.lockedRemainingMinutes
                  ? t('pit.security.account.unlockRemaining', {
                    count: status.lockedRemainingMinutes,
                    value: formatCoinNumber(status.lockedRemainingMinutes, language),
                  })
                  : t('pit.security.account.lockedSupport')
                : t('pit.security.account.normalDescription')}
            </div>
          </div>
          {status?.isLocked && (
            <div className={styles.cardAction}>
              <button className={styles.actionButton} disabled>
                {t('pit.security.account.waitUnlock')}
              </button>
            </div>
          )}
        </div>

        {/* 失败尝试次数 */}
        <div className={`${styles.securityCard} ${
          failedAttempts > 0 ? styles.warning : styles.secure
        }`}>
          <div className={styles.cardIcon}>
            {failedAttempts > 0 ? '⚠️' : '✅'}
          </div>
          <div className={styles.cardContent}>
            <div className={styles.cardTitle}>{t('pit.security.attempts.title')}</div>
            <div className={styles.cardStatus}>
              {t('pit.security.attempts.failedCount', {
                count: failedAttempts,
                value: formatCoinNumber(failedAttempts, language),
              })}
            </div>
            <div className={styles.cardDescription}>
              {t(failedAttempts > 0
                ? 'pit.security.attempts.failedDescription'
                : 'pit.security.attempts.cleanDescription', {
                count: failedAttempts,
                value: formatCoinNumber(failedAttempts, language),
              })}
            </div>
          </div>
        </div>

        {/* 最后使用时间 */}
        <div className={styles.securityCard}>
          <div className={styles.cardIcon}>⏰</div>
          <div className={styles.cardContent}>
            <div className={styles.cardTitle}>{t('pit.security.lastUsed.title')}</div>
            <div className={styles.cardStatus}>
              {status?.lastPasswordUsedTime
                ? formatCoinDateTime(status.lastPasswordUsedTime, displayTimeZone, language)
                : t('pit.security.lastUsed.never')}
            </div>
            <div className={styles.cardDescription}>
              {t('pit.security.lastUsed.description')}
            </div>
          </div>
        </div>
      </div>

      {/* 快速操作 */}
      <div className={styles.quickActions}>
        <h4 className={styles.actionsTitle}>{t('pit.overview.quickActions')}</h4>
        <div className={styles.actionsList}>
          <button className={styles.quickActionButton} onClick={() => onNavigate('password')}>
            <span className={styles.actionIcon}>🔑</span>
            <span className={styles.actionText}>
              {t(requiresPasscodeUpgrade
                ? 'pit.security.passcode.reset'
                : status?.hasPaymentPassword
                  ? 'pit.security.passcode.changeFull'
                  : 'pit.security.passcode.setFull')}
            </span>
          </button>
          <button className={styles.quickActionButton} onClick={() => onNavigate('log')}>
            <span className={styles.actionIcon}>📋</span>
            <span className={styles.actionText}>{t('pit.security.action.logs')}</span>
          </button>
          <button className={styles.quickActionButton} onClick={() => onNavigate('tips')}>
            <span className={styles.actionIcon}>💡</span>
            <span className={styles.actionText}>{t('pit.security.action.tips')}</span>
          </button>
        </div>
      </div>
    </div>
  );
};
