import { formatDateTime } from '../../utils';
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
  if (loading) {
    return (
      <div className={styles.container}>
        <div className={styles.loading}>
          <div className={styles.loadingSpinner}></div>
          <p>加载安全状态中...</p>
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

  return (
    <div className={styles.container}>
      {/* 安全评分 */}
      <div className={styles.securityScore}>
        <div className={styles.scoreHeader}>
          <h3 className={styles.scoreTitle}>
            <span className={styles.scoreIcon}>🛡️</span>
            安全评分
          </h3>
          <button className={styles.refreshButton} onClick={onRefresh} title="刷新状态">
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
              {requiresPasscodeUpgrade ? '需重置' :
               securityScore >= 80 ? '安全' :
               securityScore >= 60 ? '良好' :
               securityScore >= 40 ? '一般' : '需要改进'}
            </div>
            <p className={styles.scoreText}>
              {requiresPasscodeUpgrade ? '检测到您仍在使用已废弃的旧支付口令，请先重置为新的6位数字支付口令后再进行购买和转移。' :
               securityScore >= 80 ? '您的账户安全设置完善' :
               securityScore >= 60 ? '您的账户安全设置良好，建议进一步完善' :
               securityScore >= 40 ? '您的账户安全设置一般，建议加强防护' :
               '您的账户安全设置需要改进，请尽快完善'}
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
            <div className={styles.cardTitle}>支付口令</div>
            <div className={styles.cardStatus}>
              {requiresPasscodeUpgrade ? '旧口令已废弃' : status?.hasPaymentPassword ? '已设置' : '未设置'}
            </div>
            <div className={styles.cardDescription}>
              {requiresPasscodeUpgrade
                ? '旧支付口令不再支持商城购买和萝卜转移，请尽快重置为新的6位数字支付口令'
                : status?.hasPaymentPassword
                ? '您已设置支付口令，转移和购买操作更安全'
                : '建议设置支付口令以保护您的萝卜资产'}
            </div>
          </div>
          <div className={styles.cardAction}>
            <button className={styles.actionButton} onClick={() => onNavigate('password')}>
              {requiresPasscodeUpgrade ? '立即重置' : status?.hasPaymentPassword ? '修改' : '设置'}
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
            <div className={styles.cardTitle}>账户状态</div>
            <div className={styles.cardStatus}>
              {status?.isLocked ? '已锁定' : '正常'}
            </div>
            <div className={styles.cardDescription}>
              {status?.isLocked
                ? `账户已锁定，${status.lockedUntil ? `${Math.ceil((new Date(status.lockedUntil).getTime() - Date.now()) / 60000)}分钟后解锁` : '请联系客服'}`
                : '账户状态正常，可以正常使用'}
            </div>
          </div>
          {status?.isLocked && (
            <div className={styles.cardAction}>
              <button className={styles.actionButton} disabled>
                等待解锁
              </button>
            </div>
          )}
        </div>

        {/* 失败尝试次数 */}
        <div className={`${styles.securityCard} ${
          (status?.failedAttempts || 0) > 0 ? styles.warning : styles.secure
        }`}>
          <div className={styles.cardIcon}>
            {(status?.failedAttempts || 0) > 0 ? '⚠️' : '✅'}
          </div>
          <div className={styles.cardContent}>
            <div className={styles.cardTitle}>口令尝试</div>
            <div className={styles.cardStatus}>
              {status?.failedAttempts || 0} 次失败
            </div>
            <div className={styles.cardDescription}>
              {(status?.failedAttempts || 0) > 0
                ? `最近有${status?.failedAttempts}次口令输入错误`
                : '最近没有口令输入错误记录'}
            </div>
          </div>
        </div>

        {/* 最后使用时间 */}
        <div className={styles.securityCard}>
          <div className={styles.cardIcon}>⏰</div>
          <div className={styles.cardContent}>
            <div className={styles.cardTitle}>最后使用</div>
            <div className={styles.cardStatus}>
              {status?.lastPasswordUsedTime
                ? formatDateTime(status.lastPasswordUsedTime)
                : '从未使用'}
            </div>
            <div className={styles.cardDescription}>
              支付口令最后使用时间
            </div>
          </div>
        </div>
      </div>

      {/* 快速操作 */}
      <div className={styles.quickActions}>
        <h4 className={styles.actionsTitle}>快速操作</h4>
        <div className={styles.actionsList}>
          <button className={styles.quickActionButton} onClick={() => onNavigate('password')}>
            <span className={styles.actionIcon}>🔑</span>
            <span className={styles.actionText}>
              {requiresPasscodeUpgrade ? '重置支付口令' : status?.hasPaymentPassword ? '修改支付口令' : '设置支付口令'}
            </span>
          </button>
          <button className={styles.quickActionButton} onClick={() => onNavigate('log')}>
            <span className={styles.actionIcon}>📋</span>
            <span className={styles.actionText}>查看安全日志</span>
          </button>
          <button className={styles.quickActionButton} onClick={() => onNavigate('tips')}>
            <span className={styles.actionIcon}>💡</span>
            <span className={styles.actionText}>查看安全建议</span>
          </button>
        </div>
      </div>
    </div>
  );
};
