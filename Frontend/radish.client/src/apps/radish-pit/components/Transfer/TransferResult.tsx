import { useTranslation } from 'react-i18next';
import { DEFAULT_TIME_ZONE, getBrowserTimeZoneId } from '@/utils/dateTime';
import { formatCoinDateTime } from '../../utils';
import type { TransferResult as TransferResultType } from '../../types';
import styles from './TransferResult.module.css';
import { log } from '@/utils/logger';
import { toast } from '@radish/ui/toast';

interface TransferResultProps {
  result: TransferResultType;
  displayMode: 'carrot' | 'white';
  onStartNew: () => void;
  onResetPasscode: () => void;
  onViewHistory: () => void;
}

/**
 * 转账结果组件
 */
export const TransferResult = ({ result, onStartNew, onResetPasscode, onViewHistory }: TransferResultProps) => {
  const { t, i18n } = useTranslation();
  const language = i18n.resolvedLanguage ?? i18n.language;
  const displayTimeZone = getBrowserTimeZoneId(DEFAULT_TIME_ZONE);
  const handleCopyTransactionNo = async () => {
    if (!result.transactionNo) return;

    try {
      await navigator.clipboard.writeText(result.transactionNo);
      toast.success(t('pit.common.transactionNoCopied'));
    } catch (err) {
      log.error('TransferResult', '复制失败:', err);
      toast.error(t('pit.common.copyFailed'));
    }
  };

  return (
    <div className={styles.container}>
      <div className={styles.card}>
        <div className={styles.cardHeader}>
          <div className={`${styles.statusIcon} ${result.success ? styles.success : styles.error}`}>
            {result.success ? '✅' : '❌'}
          </div>
          <h3 className={styles.cardTitle}>
            {t(result.success ? 'pit.transfer.result.successTitle' : 'pit.transfer.result.failedTitle')}
          </h3>
          <p className={styles.cardSubtitle}>
            {result.message}
          </p>
        </div>

        <div className={styles.resultContent}>
          {result.success ? (
            <>
              {/* 成功结果 */}
              <div className={styles.successContent}>
                <div className={styles.successAnimation}>
                  <div className={styles.checkmark}>
                    <div className={styles.checkmarkCircle}></div>
                    <div className={styles.checkmarkStem}></div>
                    <div className={styles.checkmarkKick}></div>
                  </div>
                </div>

                <div className={styles.successMessage}>
                  <h4>{t('pit.transfer.result.completed')}</h4>
                  <p>{t('pit.transfer.result.completedDescription')}</p>
                </div>

                {result.transactionNo && (
                  <div className={styles.transactionInfo}>
                    <div className={styles.transactionLabel}>{t('pit.common.transactionNo')}</div>
                    <div className={styles.transactionNo}>
                      <span className={styles.transactionNoText}>{result.transactionNo}</span>
                      <button
                        className={styles.copyButton}
                        onClick={handleCopyTransactionNo}
                        title={t('pit.common.copyTransactionNo')}
                      >
                        📋
                      </button>
                    </div>
                    <div className={styles.transactionTime}>
                      {formatCoinDateTime(new Date(), displayTimeZone, language)}
                    </div>
                  </div>
                )}

                <div className={styles.nextSteps}>
                  <h5>{t('pit.transfer.result.nextSteps')}</h5>
                  <ul>
                    <li>{t('pit.transfer.result.nextHistory')}</li>
                    <li>{t('pit.transfer.result.nextTransfer')}</li>
                    <li>{t('pit.transfer.result.nextOverview')}</li>
                  </ul>
                </div>
              </div>
            </>
          ) : (
            <>
              {/* 失败结果 */}
              <div className={styles.errorContent}>
                <div className={styles.errorAnimation}>
                  <div className={styles.errorIcon}>
                    <div className={styles.errorCircle}></div>
                    <div className={styles.errorCross}>
                      <div className={styles.errorLine1}></div>
                      <div className={styles.errorLine2}></div>
                    </div>
                  </div>
                </div>

                <div className={styles.errorMessage}>
                  <h4>{t(result.requiresPasscodeUpgrade
                    ? 'pit.transfer.result.upgradeTitle'
                    : 'pit.transfer.result.failedTitle')}</h4>
                  <p>{t(result.requiresPasscodeUpgrade
                    ? 'pit.transfer.result.upgradeDescription'
                    : 'pit.transfer.result.failedDescription')}</p>
                </div>

                <div className={styles.errorDetails}>
                  <div className={styles.errorReason}>
                    <strong>{t('pit.transfer.result.reason')}</strong>
                    <span>{result.message}</span>
                  </div>
                </div>

                <div className={styles.troubleshooting}>
                  <h5>{t(result.requiresPasscodeUpgrade
                    ? 'pit.transfer.result.upgradeSuggestions'
                    : 'pit.transfer.result.solutions')}</h5>
                  <ul>
                    {result.requiresPasscodeUpgrade ? (
                      <>
                        <li>{t('pit.transfer.result.upgradeStepSecurity')}</li>
                        <li>{t('pit.transfer.result.upgradeStepRetry')}</li>
                        <li>{t('pit.transfer.result.upgradeStepLegacy')}</li>
                      </>
                    ) : (
                      <>
                        <li>{t('pit.transfer.result.solutionBalance')}</li>
                        <li>{t('pit.transfer.result.solutionRecipient')}</li>
                        <li>{t('pit.transfer.result.solutionPasscode')}</li>
                        <li>{t('pit.transfer.result.solutionRetry')}</li>
                      </>
                    )}
                  </ul>
                </div>
              </div>
            </>
          )}

          {/* 操作按钮 */}
          <div className={styles.actions}>
            {result.requiresPasscodeUpgrade && (
              <button
                type="button"
                className={styles.warningButton}
                onClick={onResetPasscode}
              >
                {t('pit.transfer.result.openSecurity')}
              </button>
            )}
            <button
              type="button"
              className={styles.primaryButton}
              onClick={onStartNew}
            >
              {t(result.success
                ? 'pit.transfer.result.continue'
                : result.requiresPasscodeUpgrade
                  ? 'pit.transfer.result.back'
                  : 'pit.transfer.result.retry')}
            </button>

            {result.success && (
              <button
                type="button"
                className={styles.secondaryButton}
                onClick={onViewHistory}
              >
                {t('pit.transfer.result.viewHistory')}
              </button>
            )}
          </div>
        </div>
      </div>
    </div>
  );
};
