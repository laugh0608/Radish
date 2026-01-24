import { formatDateTime } from '../../utils';
import type { TransferResult as TransferResultType } from '../../types';
import styles from './TransferResult.module.css';

interface TransferResultProps {
  result: TransferResultType;
  displayMode: 'carrot' | 'white';
  onStartNew: () => void;
}

/**
 * 转账结果组件
 */
export const TransferResult = ({ result, displayMode, onStartNew }: TransferResultProps) => {
  const handleCopyTransactionNo = async () => {
    if (!result.transactionNo) return;

    try {
      await navigator.clipboard.writeText(result.transactionNo);
      // TODO: 显示复制成功提示
      alert('流水号已复制到剪贴板');
    } catch (err) {
      console.error('复制失败:', err);
      alert('复制失败，请手动复制');
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
            {result.success ? '转移成功' : '转移失败'}
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
                  <h4>转移已完成</h4>
                  <p>您的萝卜币已成功转移，对方将立即收到</p>
                </div>

                {result.transactionNo && (
                  <div className={styles.transactionInfo}>
                    <div className={styles.transactionLabel}>交易流水号</div>
                    <div className={styles.transactionNo}>
                      <span className={styles.transactionNoText}>{result.transactionNo}</span>
                      <button
                        className={styles.copyButton}
                        onClick={handleCopyTransactionNo}
                        title="复制流水号"
                      >
                        📋
                      </button>
                    </div>
                    <div className={styles.transactionTime}>
                      {formatDateTime(new Date().toISOString())}
                    </div>
                  </div>
                )}

                <div className={styles.nextSteps}>
                  <h5>接下来您可以：</h5>
                  <ul>
                    <li>查看详细的交易记录</li>
                    <li>继续进行其他转移操作</li>
                    <li>返回账户总览查看余额</li>
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
                  <h4>转移失败</h4>
                  <p>很抱歉，您的转移操作未能完成</p>
                </div>

                <div className={styles.errorDetails}>
                  <div className={styles.errorReason}>
                    <strong>失败原因：</strong>
                    <span>{result.message}</span>
                  </div>
                </div>

                <div className={styles.troubleshooting}>
                  <h5>可能的解决方案：</h5>
                  <ul>
                    <li>检查账户余额是否充足</li>
                    <li>确认接收方用户信息正确</li>
                    <li>验证支付密码是否正确</li>
                    <li>稍后重试或联系客服</li>
                  </ul>
                </div>
              </div>
            </>
          )}

          {/* 操作按钮 */}
          <div className={styles.actions}>
            <button
              type="button"
              className={styles.primaryButton}
              onClick={onStartNew}
            >
              {result.success ? '继续转移' : '重新转移'}
            </button>

            {result.success && (
              <button
                type="button"
                className={styles.secondaryButton}
                onClick={() => {
                  // TODO: 跳转到交易记录页面
                  console.log('查看交易记录');
                }}
              >
                查看记录
              </button>
            )}
          </div>
        </div>
      </div>
    </div>
  );
};