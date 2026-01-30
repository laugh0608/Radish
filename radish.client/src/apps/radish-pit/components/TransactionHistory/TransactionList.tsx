import { formatCoinAmount, formatDateTime, getTransactionTypeDisplay, getTransactionStatusColor, getSafeUserDisplayName } from '../../utils';
import { useUserStore } from '@/stores/userStore';
import type { CoinTransactionVo } from '@/api/coin';
import styles from './TransactionList.module.css';

interface TransactionListProps {
  transactions: CoinTransactionVo[];
  loading: boolean;
  error: string | null;
  displayMode: 'carrot' | 'white';
  currentPage: number;
  totalPages: number;
  totalCount: number;
  onTransactionClick: (transaction: CoinTransactionVo) => void;
  onPageChange: (page: number) => void;
  onRefresh: () => void;
}

/**
 * 交易列表组件
 */
export const TransactionList = ({
  transactions,
  loading,
  error,
  displayMode,
  currentPage,
  totalPages,
  totalCount,
  onTransactionClick,
  onPageChange,
  onRefresh
}: TransactionListProps) => {
  const { userId } = useUserStore();
  const useWhiteRadish = displayMode === 'white';

  if (loading) {
    return (
      <div className={styles.container}>
        <div className={styles.loading}>
          <div className={styles.loadingSpinner}></div>
          <p>加载交易记录中...</p>
        </div>
      </div>
    );
  }

  if (error) {
    return (
      <div className={styles.container}>
        <div className={styles.error}>
          <div className={styles.errorIcon}>⚠️</div>
          <h3>加载失败</h3>
          <p>{error}</p>
          <button className={styles.retryButton} onClick={onRefresh}>
            重试
          </button>
        </div>
      </div>
    );
  }

  if (transactions.length === 0) {
    return (
      <div className={styles.container}>
        <div className={styles.empty}>
          <div className={styles.emptyIcon}>📝</div>
          <h3>暂无交易记录</h3>
          <p>当前筛选条件下没有找到交易记录</p>
          <p className={styles.emptyHint}>尝试调整筛选条件或开始使用萝卜</p>
        </div>
      </div>
    );
  }

  const renderPagination = () => {
    if (totalPages <= 1) return null;

    const pages = [];
    const maxVisiblePages = 7;
    let startPage = Math.max(1, currentPage - Math.floor(maxVisiblePages / 2));
    let endPage = Math.min(totalPages, startPage + maxVisiblePages - 1);

    if (endPage - startPage + 1 < maxVisiblePages) {
      startPage = Math.max(1, endPage - maxVisiblePages + 1);
    }

    // 首页
    if (startPage > 1) {
      pages.push(
        <button
          key={1}
          className={`${styles.pageButton} ${currentPage === 1 ? styles.active : ''}`}
          onClick={() => onPageChange(1)}
        >
          1
        </button>
      );
      if (startPage > 2) {
        pages.push(<span key="start-ellipsis" className={styles.ellipsis}>...</span>);
      }
    }

    // 中间页码
    for (let i = startPage; i <= endPage; i++) {
      pages.push(
        <button
          key={i}
          className={`${styles.pageButton} ${currentPage === i ? styles.active : ''}`}
          onClick={() => onPageChange(i)}
        >
          {i}
        </button>
      );
    }

    // 末页
    if (endPage < totalPages) {
      if (endPage < totalPages - 1) {
        pages.push(<span key="end-ellipsis" className={styles.ellipsis}>...</span>);
      }
      pages.push(
        <button
          key={totalPages}
          className={`${styles.pageButton} ${currentPage === totalPages ? styles.active : ''}`}
          onClick={() => onPageChange(totalPages)}
        >
          {totalPages}
        </button>
      );
    }

    return (
      <div className={styles.pagination}>
        <button
          className={styles.pageButton}
          onClick={() => onPageChange(currentPage - 1)}
          disabled={currentPage <= 1}
        >
          上一页
        </button>
        {pages}
        <button
          className={styles.pageButton}
          onClick={() => onPageChange(currentPage + 1)}
          disabled={currentPage >= totalPages}
        >
          下一页
        </button>
      </div>
    );
  };

  return (
    <div className={styles.container}>
      {/* 统计信息 */}
      <div className={styles.summary}>
        <div className={styles.summaryInfo}>
          第 {((currentPage - 1) * 20 + 1).toLocaleString()} - {Math.min(currentPage * 20, totalCount).toLocaleString()} 条，
          共 {totalCount.toLocaleString()} 条记录
        </div>
        <div className={styles.summaryActions}>
          <span className={styles.displayMode}>
            {displayMode === 'carrot' ? '胡萝卜模式' : '白萝卜模式'}
          </span>
        </div>
      </div>

      {/* 交易列表 */}
      <div className={styles.transactionList}>
        {transactions.map((transaction) => (
          <div
            key={transaction.voId}
            className={styles.transactionItem}
            onClick={() => onTransactionClick(transaction)}
          >
            <div className={styles.transactionIcon}>
              {getTransactionIcon(transaction.voTransactionType)}
            </div>

            <div className={styles.transactionContent}>
              <div className={styles.transactionMain}>
                <div className={styles.transactionLeft}>
                  <div className={styles.transactionType}>
                    {getTransactionTypeDisplay(transaction.voTransactionType)}
                  </div>
                  <div className={styles.transactionParties}>
                    {renderTransactionParties(transaction, userId)}
                  </div>
                </div>

                <div className={styles.transactionRight}>
                  <div className={styles.transactionAmount}>
                    <span className={`${styles.amountValue} ${
                      getAmountDirection(transaction, userId) === 'in' ? styles.positive : styles.negative
                    }`}>
                      {getAmountDirection(transaction, userId) === 'in' ? '+' : '-'}
                      {formatCoinAmount(Math.abs(transaction.voAmount), true, useWhiteRadish)}
                    </span>
                  </div>
                  <div className={`${styles.transactionStatus} ${styles[getTransactionStatusColor(transaction.voStatus)]}`}>
                    {transaction.voStatusDisplay}
                  </div>
                </div>
              </div>

              <div className={styles.transactionDetails}>
                <div className={styles.transactionTime}>
                  {formatDateTime(transaction.voCreatedAt)}
                </div>
                <div className={styles.transactionNo}>
                  流水号: {transaction.voTransactionNo}
                </div>
              </div>

              {transaction.voNote && (
                <div className={styles.transactionNote}>
                  <span className={styles.noteIcon}>💬</span>
                  {transaction.voNote}
                </div>
              )}
            </div>

            <div className={styles.transactionArrow}>
              →
            </div>
          </div>
        ))}
      </div>

      {/* 分页 */}
      {renderPagination()}
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

/**
 * 渲染交易参与方信息
 */
const renderTransactionParties = (transaction: CoinTransactionVo, currentUserId: number): string => {
  const fromUser = transaction.voFromUserName;
  const toUser = transaction.voToUserName;

  if (!fromUser && !toUser) {
    return '系统交易';
  }

  if (!fromUser) {
    return `系统 → ${getSafeUserDisplayName(toUser || '', transaction.voToUserId === currentUserId)}`;
  }

  if (!toUser) {
    return `${getSafeUserDisplayName(fromUser, transaction.voFromUserId === currentUserId)} → 系统`;
  }

  return `${getSafeUserDisplayName(fromUser, transaction.voFromUserId === currentUserId)} → ${getSafeUserDisplayName(toUser, transaction.voToUserId === currentUserId)}`;
};

/**
 * 获取金额方向（收入/支出）
 */
const getAmountDirection = (transaction: CoinTransactionVo, currentUserId: number): 'in' | 'out' => {
  // 如果当前用户是接收方，则为收入
  if (transaction.voToUserId === currentUserId) {
    return 'in';
  }
  // 如果当前用户是发送方，则为支出
  if (transaction.voFromUserId === currentUserId) {
    return 'out';
  }
  // 系统交易，根据金额正负判断
  return transaction.voAmount > 0 ? 'in' : 'out';
};