import { formatCoinAmount, getSafeUserDisplayName } from '../../utils';
import { useTranslation } from 'react-i18next';
import type { TransferFormData } from '../../types';
import styles from './TransferConfirm.module.css';

interface TransferConfirmProps {
  transferData: TransferFormData;
  displayMode: 'carrot' | 'white';
  loading: boolean;
  onConfirm: () => void;
  onCancel: () => void;
}

/**
 * 转账确认组件
 */
export const TransferConfirm = ({
  transferData,
  displayMode,
  loading,
  onConfirm,
  onCancel
}: TransferConfirmProps) => {
  const { t, i18n } = useTranslation();
  const language = i18n.resolvedLanguage ?? i18n.language;
  const recipientName = getSafeUserDisplayName(transferData.recipientName, false, t('pit.common.me'));

  return (
    <div className={styles.container}>
      <div className={styles.card}>
        <div className={styles.cardHeader}>
          <h3 className={styles.cardTitle}>
            <span className={styles.cardIcon}>🔍</span>
            {t('pit.transfer.confirm.title')}
          </h3>
          <p className={styles.cardSubtitle}>{t('pit.transfer.confirm.description')}</p>
        </div>

        <div className={styles.confirmContent}>
          {/* 转移概览 */}
          <div className={styles.transferOverview}>
            <div className={styles.transferFlow}>
              <div className={styles.transferParty}>
                <div className={styles.partyIcon}>👤</div>
                <div className={styles.partyInfo}>
                  <div className={styles.partyLabel}>{t('pit.transfer.confirm.sender')}</div>
                  <div className={styles.partyName}>{t('pit.common.me')}</div>
                </div>
              </div>

              <div className={styles.transferArrow}>
                <div className={styles.arrowLine}></div>
                <div className={styles.arrowHead}>→</div>
                <div className={styles.transferAmount}>
                  {formatCoinAmount(transferData.amount, language, t, displayMode)}
                </div>
              </div>

              <div className={styles.transferParty}>
                <div className={styles.partyIcon}>👥</div>
                <div className={styles.partyInfo}>
                  <div className={styles.partyLabel}>{t('pit.transfer.confirm.recipient')}</div>
                  <div className={styles.partyName}>
                    {recipientName}
                  </div>
                </div>
              </div>
            </div>
          </div>

          {/* 转移详情 */}
          <div className={styles.transferDetails}>
            <h4 className={styles.detailsTitle}>{t('pit.transfer.confirm.details')}</h4>
            <div className={styles.detailsList}>
              <div className={styles.detailItem}>
                <div className={styles.detailLabel}>{t('pit.transfer.confirm.recipientUser')}</div>
                <div className={styles.detailValue}>
                  {recipientName}
                </div>
              </div>

              <div className={styles.detailItem}>
                <div className={styles.detailLabel}>{t('pit.transfer.confirm.amount')}</div>
                <div className={`${styles.detailValue} ${styles.amountValue}`}>
                  {formatCoinAmount(transferData.amount, language, t, displayMode)}
                </div>
              </div>

              <div className={styles.detailItem}>
                <div className={styles.detailLabel}>{t('pit.transfer.confirm.fee')}</div>
                <div className={styles.detailValue}>
                  {t('pit.transfer.confirm.free')}
                </div>
              </div>

              <div className={styles.detailItem}>
                <div className={styles.detailLabel}>{t('pit.transfer.confirm.receivedAmount')}</div>
                <div className={`${styles.detailValue} ${styles.amountValue}`}>
                  {formatCoinAmount(transferData.amount, language, t, displayMode)}
                </div>
              </div>

              {transferData.note && (
                <div className={styles.detailItem}>
                  <div className={styles.detailLabel}>{t('pit.transfer.confirm.remark')}</div>
                  <div className={styles.detailValue}>
                    {transferData.note}
                  </div>
                </div>
              )}

              <div className={styles.detailItem}>
                <div className={styles.detailLabel}>{t('pit.transfer.confirm.arrival')}</div>
                <div className={styles.detailValue}>
                  {t('pit.transfer.confirm.instant')}
                </div>
              </div>
            </div>
          </div>

          {/* 安全提示 */}
          <div className={styles.securityTips}>
            <div className={styles.tipsHeader}>
              <span className={styles.tipsIcon}>🔒</span>
              <span className={styles.tipsTitle}>{t('pit.transfer.confirm.securityTitle')}</span>
            </div>
            <ul className={styles.tipsList}>
              <li>{t('pit.transfer.confirm.securityRecipient')}</li>
              <li>{t('pit.transfer.confirm.securityDebit')}</li>
              <li>{t('pit.transfer.confirm.securitySupport')}</li>
            </ul>
          </div>

          {/* 操作按钮 */}
          <div className={styles.actions}>
            <button
              type="button"
              className={styles.cancelButton}
              onClick={onCancel}
              disabled={loading}
            >
              {t('pit.transfer.confirm.cancel')}
            </button>
            <button
              type="button"
              className={styles.confirmButton}
              onClick={onConfirm}
              disabled={loading}
            >
              {loading ? (
                <>
                  <div className={styles.buttonSpinner}></div>
                  {t('pit.common.processing')}
                </>
              ) : (
                t('pit.transfer.confirm.submit')
              )}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};
