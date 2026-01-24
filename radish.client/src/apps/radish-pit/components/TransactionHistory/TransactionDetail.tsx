import { formatCoinAmount, formatDateTime, getTransactionTypeDisplay, getTransactionStatusColor, getSafeUserDisplayName } from '../../utils';
import { useUserStore } from '@/stores/userStore';
import type { CoinTransactionVo } from '@/api/coin';
import styles from './TransactionDetail.module.css';

interface TransactionDetailProps {
  transaction: CoinTransactionVo;
  displayMode: 'carrot' | 'white';
  onClose: () => void;
}

/**
 * 交易详情组件
 */
export const TransactionDetail = ({ transaction, displayMode, onClose }: TransactionDetailProps) => {
  const { userId } = useUserStore();
  const useWhiteRadish = displayMode === 'white';

  const handleBackdropClick = (e: React.MouseEvent) => {
    if (e.target === e.currentTarget) {
      onClose();
    }
  };

  const handleCopyTransactionNo = async () => {
    try {
      await navigator.clipboard.writeText(transaction.voTransactionNo);
      // TODO: 显示复制成功提示
      alert('流水号已复制到剪贴板');
    } catch (err) {
      console.error('复制失败:', err);
      alert('复制失败，请手动复制');
    }
  };

  const getAmountDirection = (): 'in' | 'out' => {
    if (transaction.voToUserId === userId) return 'in';
    if (transaction.voFromUserId === userId) return 'out';
    return transaction.voAmount > 0 ? 'in' : 'out';
  };

  const renderParticipants = () => {
    const fromUser = transaction.voFromUserName;
    const toUser = transaction.voToUserName;

    return (
      <div className={styles.participants}>
        <div className={styles.participantItem}>
          <div className={styles.participantLabel}>发起方</div>
          <div className={styles.participantValue}>
            {fromUser ? (
              <span className={transaction.voFromUserId === userId ? styles.currentUser : ''}>
                {getSafeUserDisplayName(fromUser, transaction.voFromUserId === userId)}
                {transaction.voFromUserId === userId && ' (我)'}
              </span>
            ) : (
              <span className={styles.systemUser}>系统</span>
            )}
          </div>
        </div>

        <div className={styles.arrow}>→</div>

        <div className={styles.participantItem}>
          <div className={styles.participantLabel}>接收方</div>
          <div className={styles.participantValue}>
            {toUser ? (
              <span className={transaction.voToUserId === userId ? styles.currentUser : ''}>
                {getSafeUserDisplayName(toUser, transaction.voToUserId === userId)}
                {transaction.voToUserId === userId && ' (我)'}
              </span>
            ) : (
              <span className={styles.systemUser}>系统</span>
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
              <h3 className={styles.title}>交易详情</h3>
              <div className={styles.transactionType}>
                {getTransactionTypeDisplay(transaction.voTransactionType)}
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
            <div className={styles.amountLabel}>交易金额</div>
            <div className={`${styles.amountValue} ${
              getAmountDirection() === 'in' ? styles.positive : styles.negative
            }`}>
              {getAmountDirection() === 'in' ? '+' : '-'}
              {formatCoinAmount(Math.abs(transaction.voAmount), true, useWhiteRadish)}
            </div>
            {transaction.voFee && transaction.voFee > 0 && (
              <div className={styles.feeInfo}>
                手续费: {formatCoinAmount(transaction.voFee, true, useWhiteRadish)}
              </div>
            )}
          </div>

          {/* 状态信息 */}
          <div className={styles.statusSection}>
            <div className={`${styles.statusBadge} ${styles[getTransactionStatusColor(transaction.voStatus)]}`}>
              {transaction.voStatusDisplay}
            </div>
            <div className={styles.statusTime}>
              {formatDateTime(transaction.voCreatedAt)}
            </div>
          </div>

          {/* 参与方信息 */}
          <div className={styles.section}>
            <h4 className={styles.sectionTitle}>参与方</h4>
            {renderParticipants()}
          </div>

          {/* 交易信息 */}
          <div className={styles.section}>
            <h4 className={styles.sectionTitle}>交易信息</h4>
            <div className={styles.infoGrid}>
              <div className={styles.infoItem}>
                <div className={styles.infoLabel}>流水号</div>
                <div className={styles.infoValue}>
                  <span className={styles.transactionNo}>{transaction.voTransactionNo}</span>
                  <button
                    className={styles.copyButton}
                    onClick={handleCopyTransactionNo}
                    title="复制流水号"
                  >
                    📋
                  </button>
                </div>
              </div>

              <div className={styles.infoItem}>
                <div className={styles.infoLabel}>创建时间</div>
                <div className={styles.infoValue}>
                  {new Date(transaction.voCreatedAt).toLocaleString('zh-CN')}
                </div>
              </div>

              {transaction.voBusinessType && (
                <div className={styles.infoItem}>
                  <div className={styles.infoLabel}>业务类型</div>
                  <div className={styles.infoValue}>{transaction.voBusinessType}</div>
                </div>
              )}

              {transaction.voBusinessId && (
                <div className={styles.infoItem}>
                  <div className={styles.infoLabel}>业务ID</div>
                  <div className={styles.infoValue}>{transaction.voBusinessId}</div>
                </div>
              )}
            </div>
          </div>

          {/* 备注信息 */}
          {transaction.voNote && (
            <div className={styles.section}>
              <h4 className={styles.sectionTitle}>备注</h4>
              <div className={styles.noteContent}>
                <span className={styles.noteIcon}>💬</span>
                {transaction.voNote}
              </div>
            </div>
          )}

          {/* 金额计算详情 */}
          {(transaction.voTheoreticalAmount || transaction.voRoundingDiff) && (
            <div className={styles.section}>
              <h4 className={styles.sectionTitle}>计算详情</h4>
              <div className={styles.calculationDetails}>
                {transaction.voTheoreticalAmount && (
                  <div className={styles.calculationItem}>
                    <span className={styles.calculationLabel}>理论金额:</span>
                    <span className={styles.calculationValue}>
                      {formatCoinAmount(transaction.voTheoreticalAmount, true, useWhiteRadish)}
                    </span>
                  </div>
                )}
                {transaction.voRoundingDiff && (
                  <div className={styles.calculationItem}>
                    <span className={styles.calculationLabel}>舍入差额:</span>
                    <span className={styles.calculationValue}>
                      {formatCoinAmount(transaction.voRoundingDiff, true, useWhiteRadish)}
                    </span>
                  </div>
                )}
              </div>
            </div>
          )}
        </div>

        <div className={styles.footer}>
          <button className={styles.closeFooterButton} onClick={onClose}>
            关闭
          </button>
        </div>
      </div>
    </div>
  );
};

/**
 * 根据交易类型获取图标
 */
const getTransactionIcon = (transactionType: string): string => {
  const iconMap: Record<string, string> = {
    'SYSTEM_GRANT': '🎁',
    'LIKE_REWARD': '👍',
    'COMMENT_REWARD': '💬',
    'GODLIKE_REWARD': '⭐',
    'SOFA_REWARD': '🛋️',
    'TRANSFER_IN': '📥',
    'TRANSFER_OUT': '📤',
    'PURCHASE': '🛒',
    'ADMIN_ADJUST': '⚙️'
  };

  return iconMap[transactionType] || '💰';
};