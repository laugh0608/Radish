import type { SecurityStatus } from '../../types';
import styles from './SecurityTips.module.css';

interface SecurityTipsProps {
  status: SecurityStatus | null;
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
export const SecurityTips = ({ status }: SecurityTipsProps) => {
  const generateTips = (): SecurityTip[] => {
    const tips: SecurityTip[] = [];

    // 基于状态生成个性化建议
    if (!status?.hasPaymentPassword) {
      tips.push({
        id: 'set-password',
        icon: '🔑',
        title: '设置支付密码',
        description: '设置支付密码可以保护您的萝卜转移操作，防止未经授权的转移。',
        priority: 'high',
        actionText: '立即设置',
        onAction: () => {
          // TODO: 跳转到支付密码设置
          console.log('跳转到支付密码设置');
        }
      });
    }

    if (status?.failedAttempts && status.failedAttempts > 0) {
      tips.push({
        id: 'failed-attempts',
        icon: '⚠️',
        title: '密码输入错误',
        description: `检测到${status.failedAttempts}次密码输入错误，请确保您的密码安全，如有异常请及时修改密码。`,
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
        title: '使用强密码',
        description: '使用包含大小写字母、数字和特殊字符的复杂密码，长度至少8位，提高账户安全性。',
        priority: 'medium'
      },
      {
        id: 'regular-change',
        icon: '🔄',
        title: '定期更换密码',
        description: '建议每3-6个月更换一次支付密码，避免长期使用同一密码带来的安全风险。',
        priority: 'medium'
      },
      {
        id: 'secure-environment',
        icon: '🏠',
        title: '安全环境操作',
        description: '在安全的网络环境下进行萝卜操作，避免在公共WiFi或不信任的设备上输入密码。',
        priority: 'medium'
      },
      {
        id: 'phishing-awareness',
        icon: '🎣',
        title: '防范钓鱼攻击',
        description: '注意识别钓鱼网站和诈骗信息，官方不会通过邮件或短信要求您提供密码信息。',
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
                <h5>密码安全</h5>
                <p>使用复杂密码，避免使用生日、姓名等容易猜测的信息作为密码。</p>
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
              <p>如果您遇到安全问题或需要技术支持，请及时联系我们的客服团队。</p>
              <div className={styles.supportActions}>
                <button className={styles.supportButton}>
                  联系客服
                </button>
                <button className={styles.supportButton}>
                  查看帮助文档
                </button>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};