import { useTranslation } from 'react-i18next';
import { DEFAULT_TIME_ZONE, getBrowserTimeZoneId } from '@/utils/dateTime';
import {
  absoluteCoinValue,
  compareCoinValues,
  formatCoinAmount,
  formatCoinDateTime,
  formatTransactionStatus,
  formatTransactionType,
  getSafeUserDisplayName,
  getTransactionIcon,
  getTransactionStatusTone,
  resolveTransactionDirection,
} from '../../utils';
import { useUserStore } from '@/stores/userStore';
import type { CoinTransaction } from '@/api/coin';
import styles from './TransactionDetail.module.css';
import { log } from '@/utils/logger';
import { toast } from '@radish/ui/toast';

interface TransactionDetailProps {
  transaction: CoinTransaction;
  displayMode: 'carrot' | 'white';
  onClose: () => void;
}

/**
 * 交易详情组件
 */
export const TransactionDetail = ({ transaction, displayMode, onClose }: TransactionDetailProps) => {
  const { t, i18n } = useTranslation();
  const language = i18n.resolvedLanguage ?? i18n.language;
  const displayTimeZone = getBrowserTimeZoneId(DEFAULT_TIME_ZONE);
  const { userId } = useUserStore();
  const currentUserId = String(userId);

  const handleBackdropClick = (e: React.MouseEvent) => {
    if (e.target === e.currentTarget) {
      onClose();
    }
  };

  const handleCopyTransactionNo = async () => {
    try {
      await navigator.clipboard.writeText(transaction.voTransactionNo);
      toast.success(t('pit.common.transactionNoCopied'));
    } catch (err) {
      log.error('TransactionDetail', '复制失败:', err);
      toast.error(t('pit.common.copyFailed'));
    }
  };

  const getAmountDirection = (): 'in' | 'out' => {
    return resolveTransactionDirection(transaction, currentUserId);
  };

  const renderParticipants = () => {
    const fromUser = transaction.voFromUserId ? transaction.voFromUserName : null;
    const toUser = transaction.voToUserId ? transaction.voToUserName : null;

    return (
      <div className={styles.participants}>
        <div className={styles.participantItem}>
          <div className={styles.participantLabel}>{t('pit.history.detail.sender')}</div>
          <div className={styles.participantValue}>
            {fromUser ? (
              <span className={transaction.voFromUserId === currentUserId ? styles.currentUser : ''}>
                {getSafeUserDisplayName(fromUser, transaction.voFromUserId === currentUserId, t('pit.common.me'))}
              </span>
            ) : (
              <span className={styles.systemUser}>{t('pit.common.system')}</span>
            )}
          </div>
        </div>

        <div className={styles.arrow}>→</div>

        <div className={styles.participantItem}>
          <div className={styles.participantLabel}>{t('pit.history.detail.recipient')}</div>
          <div className={styles.participantValue}>
            {toUser ? (
              <span className={transaction.voToUserId === currentUserId ? styles.currentUser : ''}>
                {getSafeUserDisplayName(toUser, transaction.voToUserId === currentUserId, t('pit.common.me'))}
              </span>
            ) : (
              <span className={styles.systemUser}>{t('pit.common.system')}</span>
            )}
          </div>
        </div>
      </div>
    );
  };

  return (
    <div className={styles.backdrop} onClick={handleBackdropClick}>
      <div className={styles.modal}>
        <div className={styles.header}>
          <div className={styles.headerLeft}>
            <div className={styles.transactionIcon}>
              {getTransactionIcon(transaction.voTransactionType)}
            </div>
            <div className={styles.headerInfo}>
              <h3 className={styles.title}>{t('pit.history.detail.title')}</h3>
              <div className={styles.transactionType}>
                {formatTransactionType(transaction.voTransactionType, t)}
              </div>
            </div>
          </div>
          <button className={styles.closeButton} onClick={onClose}>
            ✕
          </button>
        </div>

        <div className={styles.content}>
          {/* 金额信息 */}
          <div className={styles.amountSection}>
            <div className={styles.amountLabel}>{t('pit.history.detail.amount')}</div>
            <div className={`${styles.amountValue} ${
              getAmountDirection() === 'in' ? styles.positive : styles.negative
            }`}>
              {getAmountDirection() === 'in' ? '+' : '-'}
              {formatCoinAmount(absoluteCoinValue(transaction.voAmount), language, t, displayMode)}
            </div>
              {compareCoinValues(transaction.voFee, 0) > 0 && (
              <div className={styles.feeInfo}>
                {t('pit.history.detail.feeValue', {
                  value: formatCoinAmount(transaction.voFee, language, t, displayMode),
                })}
              </div>
            )}
          </div>

          {/* 状态信息 */}
          <div className={styles.statusSection}>
            <div className={`${styles.statusBadge} ${styles[getTransactionStatusTone(transaction.voStatus)]}`}>
              {formatTransactionStatus(transaction.voStatus, t)}
            </div>
            <div className={styles.statusTime}>
              {formatCoinDateTime(transaction.voCreateTime, displayTimeZone, language)}
            </div>
          </div>

          {/* 参与方信息 */}
          <div className={styles.section}>
            <h4 className={styles.sectionTitle}>{t('pit.history.detail.participants')}</h4>
            {renderParticipants()}
          </div>

          {/* 交易信息 */}
          <div className={styles.section}>
            <h4 className={styles.sectionTitle}>{t('pit.history.detail.info')}</h4>
            <div className={styles.infoGrid}>
              <div className={styles.infoItem}>
                <div className={styles.infoLabel}>{t('pit.common.transactionNo')}</div>
                <div className={styles.infoValue}>
                  <span className={styles.transactionNo}>{transaction.voTransactionNo}</span>
                  <button
                    className={styles.copyButton}
                    onClick={handleCopyTransactionNo}
                    title={t('pit.common.copyTransactionNo')}
                  >
                    📋
                  </button>
                </div>
              </div>

              <div className={styles.infoItem}>
                <div className={styles.infoLabel}>{t('pit.history.detail.createdAt')}</div>
                <div className={styles.infoValue}>
                  {formatCoinDateTime(transaction.voCreateTime, displayTimeZone, language)}
                </div>
              </div>

              {transaction.voBusinessType && (
                <div className={styles.infoItem}>
                  <div className={styles.infoLabel}>{t('pit.history.detail.businessType')}</div>
                  <div className={styles.infoValue}>{transaction.voBusinessType}</div>
                </div>
              )}

              {transaction.voBusinessId && (
                <div className={styles.infoItem}>
                  <div className={styles.infoLabel}>{t('pit.history.detail.businessId')}</div>
                  <div className={styles.infoValue}>{transaction.voBusinessId}</div>
                </div>
              )}
            </div>
          </div>

          {/* 备注信息 */}
          {transaction.voRemark && (
            <div className={styles.section}>
              <h4 className={styles.sectionTitle}>{t('pit.history.detail.remark')}</h4>
              <div className={styles.noteContent}>
                <span className={styles.noteIcon}>💬</span>
                {transaction.voRemark}
              </div>
            </div>
          )}

          {/* 金额计算详情 */}
          {(transaction.voTheoreticalAmount || transaction.voRoundingDiff) && (
            <div className={styles.section}>
              <h4 className={styles.sectionTitle}>{t('pit.history.detail.calculation')}</h4>
              <div className={styles.calculationDetails}>
                {transaction.voTheoreticalAmount && (
                  <div className={styles.calculationItem}>
                    <span className={styles.calculationLabel}>{t('pit.history.detail.theoretical')}</span>
                    <span className={styles.calculationValue}>
                      {formatCoinAmount(transaction.voTheoreticalAmount, language, t, displayMode)}
                    </span>
                  </div>
                )}
                {transaction.voRoundingDiff && (
                  <div className={styles.calculationItem}>
                    <span className={styles.calculationLabel}>{t('pit.history.detail.rounding')}</span>
                    <span className={styles.calculationValue}>
                      {formatCoinAmount(transaction.voRoundingDiff, language, t, displayMode)}
                    </span>
                  </div>
                )}
              </div>
            </div>
          )}
        </div>

        <div className={styles.footer}>
          <button className={styles.closeFooterButton} onClick={onClose}>
            {t('pit.common.close')}
          </button>
        </div>
      </div>
    </div>
  );
};
