import { formatCoinAmount, getSafeUserDisplayName } from '../../utils';
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
  const useWhiteRadish = displayMode === 'white';

  return (
    <div className={styles.container}>
      <div className={styles.card}>
        <div className={styles.cardHeader}>
          <h3 className={styles.cardTitle}>
            <span className={styles.cardIcon}>🔍</span>
            确认转移信息
          </h3>
          <p className={styles.cardSubtitle}>请仔细核对以下转移信息，确认无误后点击确认转移</p>
        </div>

        <div className={styles.confirmContent}>
          {/* 转移概览 */}
          <div className={styles.transferOverview}>
            <div className={styles.transferFlow}>
              <div className={styles.transferParty}>
                <div className={styles.partyIcon}>👤</div>
                <div className={styles.partyInfo}>
                  <div className={styles.partyLabel}>转出方</div>
                  <div className={styles.partyName}>我</div>
                </div>
              </div>

              <div className={styles.transferArrow}>
                <div className={styles.arrowLine}></div>
                <div className={styles.arrowHead}>→</div>
                <div className={styles.transferAmount}>
                  {formatCoinAmount(transferData.amount, true, useWhiteRadish)}
                </div>
              </div>

              <div className={styles.transferParty}>
                <div className={styles.partyIcon}>👥</div>
                <div className={styles.partyInfo}>
                  <div className={styles.partyLabel}>接收方</div>
                  <div className={styles.partyName}>
                    {getSafeUserDisplayName(transferData.recipientName)}
                  </div>
                </div>
              </div>
            </div>
          </div>

          {/* 转移详情 */}
          <div className={styles.transferDetails}>
            <h4 className={styles.detailsTitle}>转移详情</h4>
            <div className={styles.detailsList}>
              <div className={styles.detailItem}>
                <div className={styles.detailLabel}>接收方用户</div>
                <div className={styles.detailValue}>
                  {getSafeUserDisplayName(transferData.recipientName)}
                </div>
              </div>

              <div className={styles.detailItem}>
                <div className={styles.detailLabel}>转移金额</div>
                <div className={`${styles.detailValue} ${styles.amountValue}`}>
                  {formatCoinAmount(transferData.amount, true, useWhiteRadish)}
                </div>
              </div>

              <div className={styles.detailItem}>
                <div className={styles.detailLabel}>手续费</div>
                <div className={styles.detailValue}>
                  免费
                </div>
              </div>

              <div className={styles.detailItem}>
                <div className={styles.detailLabel}>实际到账</div>
                <div className={`${styles.detailValue} ${styles.amountValue}`}>
                  {formatCoinAmount(transferData.amount, true, useWhiteRadish)}
                </div>
              </div>

              {transferData.note && (
                <div className={styles.detailItem}>
                  <div className={styles.detailLabel}>转移备注</div>
                  <div className={styles.detailValue}>
                    {transferData.note}
                  </div>
                </div>
              )}

              <div className={styles.detailItem}>
                <div className={styles.detailLabel}>预计到账时间</div>
                <div className={styles.detailValue}>
                  即时到账
                </div>
              </div>
            </div>
          </div>

          {/* 安全提示 */}
          <div className={styles.securityTips}>
            <div className={styles.tipsHeader}>
              <span className={styles.tipsIcon}>🔒</span>
              <span className={styles.tipsTitle}>安全提示</span>
            </div>
            <ul className={styles.tipsList}>
              <li>请确认接收方用户信息正确，转移后无法撤销</li>
              <li>转移完成后，萝卜将立即从您的账户扣除</li>
              <li>如有疑问，请联系客服或取消本次转移</li>
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
              取消转移
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
                  处理中...
                </>
              ) : (
                '确认转移'
              )}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};