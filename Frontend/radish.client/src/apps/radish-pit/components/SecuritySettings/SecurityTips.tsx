import type { SecurityStatus } from '../../types';
import styles from './SecurityTips.module.css';
import { log } from '@/utils/logger';

type SecurityActionTab = 'overview' | 'password' | 'log';

interface SecurityTipsProps {
  status: SecurityStatus | null;
  onNavigate: (tab: SecurityActionTab) => void;
}

interface SecurityTip {
  id: string;
  icon: string;
  title: string;
  description: string;
  priority: 'high' | 'medium' | 'low';
  actionText?: string;
  onAction?: () => void;
}

/**
 * 安全建议组件
 */
export const SecurityTips = ({ status, onNavigate }: SecurityTipsProps) => {
  const navigateTo = (tab: SecurityActionTab, source: string) => {
    log.debug('SecurityTips', `从 ${source} 跳转到 ${tab}`);
    onNavigate(tab);
  };

  const generateTips = (): SecurityTip[] => {
    const tips: SecurityTip[] = [];

    // 基于状态生成个性化建议
    if (!status?.hasPaymentPassword) {
      tips.push({
        id: 'set-password',
        icon: '🔑',
        title: '设置支付口令',
        description: '设置 6 位数字支付口令，可以保护您的萝卜转移和购买操作，防止未经授权的消费。',
        priority: 'high',
        actionText: '立即设置',
        onAction: () => navigateTo('password', 'tip:set-password')
      });
    }

    if (status?.requiresPasscodeUpgrade) {
      tips.push({
        id: 'upgrade-legacy-passcode',
        icon: '♻️',
        title: '重置旧支付口令',
        description: '检测到您的账户仍在使用旧支付口令，旧规则已废弃，商城购买和萝卜转移都会被阻止，请立即重置为新的 6 位数字支付口令。',
        priority: 'high',
        actionText: '立即重置',
        onAction: () => navigateTo('password', 'tip:upgrade-legacy-passcode')
      });
    }

    if (status?.failedAttempts && status.failedAttempts > 0) {
      tips.push({
        id: 'failed-attempts',
        icon: '⚠️',
        title: '口令输入错误',
        description: `检测到${status.failedAttempts}次口令输入错误，请确认是否为本人操作，如有异常请及时修改支付口令。`,
        priority: 'high'
      });
    }

    if (status?.isLocked) {
      tips.push({
        id: 'account-locked',
        icon: '🔒',
        title: '账户已锁定',
        description: '由于多次密码输入错误，您的账户已被临时锁定。请等待锁定时间结束或联系客服。',
        priority: 'high'
      });
    }

    // 通用安全建议
    tips.push(
      {
        id: 'strong-password',
        icon: '💪',
        title: '设置稳妥口令',
        description: '支付口令固定为 6 位数字，避免使用连续数字或重复数字，提升资产操作安全性。',
        priority: 'medium'
      },
      {
        id: 'regular-change',
        icon: '🔄',
        title: '定期更换口令',
        description: '建议每3-6个月更换一次支付口令，避免长期使用同一口令带来的安全风险。',
        priority: 'medium'
      },
      {
        id: 'secure-environment',
        icon: '🏠',
        title: '安全环境操作',
        description: '在安全的网络环境下进行萝卜操作，避免在公共 WiFi 或不信任设备上输入支付口令。',
        priority: 'medium'
      },
      {
        id: 'phishing-awareness',
        icon: '🎣',
        title: '防范钓鱼攻击',
        description: '注意识别钓鱼网站和诈骗信息，官方不会通过邮件或短信要求您提供支付口令。',
        priority: 'medium'
      },
      {
        id: 'monitor-activity',
        icon: '👀',
        title: '监控账户活动',
        description: '定期查看安全日志和交易记录，如发现异常活动请及时联系客服处理。',
        priority: 'low'
      },
      {
        id: 'backup-info',
        icon: '💾',
        title: '备份重要信息',
        description: '妥善保管您的账户信息，建议将重要信息备份到安全的地方，避免遗失。',
        priority: 'low'
      }
    );

    return tips;
  };

  const tips = generateTips();
  const highPriorityTips = tips.filter(tip => tip.priority === 'high');
  const mediumPriorityTips = tips.filter(tip => tip.priority === 'medium');
  const lowPriorityTips = tips.filter(tip => tip.priority === 'low');

  const renderTipSection = (title: string, tips: SecurityTip[], className: string) => {
    if (tips.length === 0) return null;

    return (
      <div className={styles.tipSection}>
        <h4 className={`${styles.sectionTitle} ${styles[className]}`}>
          {title} ({tips.length})
        </h4>
        <div className={styles.tipsList}>
          {tips.map((tip) => (
            <div key={tip.id} className={`${styles.tipCard} ${styles[tip.priority]}`}>
              <div className={styles.tipIcon}>{tip.icon}</div>
              <div className={styles.tipContent}>
                <div className={styles.tipHeader}>
                  <h5 className={styles.tipTitle}>{tip.title}</h5>
                  <div className={`${styles.tipPriority} ${styles[tip.priority]}`}>
                    {tip.priority === 'high' ? '重要' :
                     tip.priority === 'medium' ? '建议' : '提示'}
                  </div>
                </div>
                <p className={styles.tipDescription}>{tip.description}</p>
                {tip.actionText && tip.onAction && (
                  <button
                    className={styles.tipAction}
                    onClick={tip.onAction}
                  >
                    {tip.actionText}
                  </button>
                )}
              </div>
            </div>
          ))}
        </div>
      </div>
    );
  };

  return (
    <div className={styles.container}>
      <div className={styles.header}>
        <h3 className={styles.title}>
          <span className={styles.titleIcon}>💡</span>
          安全建议
        </h3>
        <p className={styles.subtitle}>
          根据您的账户状态，我们为您提供以下安全建议
        </p>
      </div>

      <div className={styles.content}>
        {renderTipSection('紧急建议', highPriorityTips, 'high')}
        {renderTipSection('重要建议', mediumPriorityTips, 'medium')}
        {renderTipSection('一般建议', lowPriorityTips, 'low')}

        {/* 安全知识 */}
        <div className={styles.knowledgeSection}>
          <h4 className={styles.knowledgeTitle}>
            <span className={styles.knowledgeIcon}>📚</span>
            安全知识
          </h4>
          <div className={styles.knowledgeCards}>
            <div className={styles.knowledgeCard}>
              <div className={styles.knowledgeCardIcon}>🔐</div>
              <div className={styles.knowledgeCardContent}>
                <h5>口令安全</h5>
                <p>支付口令只用于资金类操作，避免使用连续数字、重复数字等容易猜测的组合。</p>
              </div>
            </div>
            <div className={styles.knowledgeCard}>
              <div className={styles.knowledgeCardIcon}>🌐</div>
              <div className={styles.knowledgeCardContent}>
                <h5>网络安全</h5>
                <p>在安全的网络环境下操作，避免在公共场所连接不安全的WiFi。</p>
              </div>
            </div>
            <div className={styles.knowledgeCard}>
              <div className={styles.knowledgeCardIcon}>📱</div>
              <div className={styles.knowledgeCardContent}>
                <h5>设备安全</h5>
                <p>保持设备系统和浏览器更新，安装可靠的安全软件。</p>
              </div>
            </div>
          </div>
        </div>

        {/* 联系支持 */}
        <div className={styles.supportSection}>
          <div className={styles.supportCard}>
            <div className={styles.supportIcon}>🆘</div>
            <div className={styles.supportContent}>
              <h4>需要帮助？</h4>
              <p>您可以先通过支付口令设置和安全日志自查常见安全问题；如仍发现异常，再联系支持处理。</p>
              <div className={styles.supportActions}>
                <button
                  type="button"
                  className={styles.supportButton}
                  onClick={() => navigateTo('password', 'support:password')}
                >
                  {status?.hasPaymentPassword ? '修改支付口令' : '设置支付口令'}
                </button>
                <button
                  type="button"
                  className={styles.supportButton}
                  onClick={() => navigateTo('log', 'support:log')}
                >
                  查看安全日志
                </button>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};
