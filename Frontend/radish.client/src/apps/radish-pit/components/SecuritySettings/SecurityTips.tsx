import type { SecurityStatus } from '../../types';
import styles from './SecurityTips.module.css';
import { log } from '@/utils/logger';
import { useTranslation } from 'react-i18next';
import { formatCoinNumber } from '../../utils';

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
  const { t, i18n } = useTranslation();
  const language = i18n.resolvedLanguage ?? i18n.language;
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
        title: t('pit.security.tips.setPasscode.title'),
        description: t('pit.security.tips.setPasscode.description'),
        priority: 'high',
        actionText: t('pit.security.passcode.setNow'),
        onAction: () => navigateTo('password', 'tip:set-password')
      });
    }

    if (status?.requiresPasscodeUpgrade) {
      tips.push({
        id: 'upgrade-legacy-passcode',
        icon: '♻️',
        title: t('pit.security.tips.upgrade.title'),
        description: t('pit.security.tips.upgrade.description'),
        priority: 'high',
        actionText: t('pit.security.passcode.resetNow'),
        onAction: () => navigateTo('password', 'tip:upgrade-legacy-passcode')
      });
    }

    if (status?.failedAttempts && status.failedAttempts > 0) {
      tips.push({
        id: 'failed-attempts',
        icon: '⚠️',
        title: t('pit.security.tips.failedAttempts.title'),
        description: t('pit.security.tips.failedAttempts.description', {
          count: status.failedAttempts,
          value: formatCoinNumber(status.failedAttempts, language),
        }),
        priority: 'high'
      });
    }

    if (status?.isLocked) {
      tips.push({
        id: 'account-locked',
        icon: '🔒',
        title: t('pit.security.tips.locked.title'),
        description: t('pit.security.tips.locked.description'),
        priority: 'high'
      });
    }

    // 通用安全建议
    tips.push(
      {
        id: 'strong-password',
        icon: '💪',
        title: t('pit.security.tips.strong.title'),
        description: t('pit.security.tips.strong.description'),
        priority: 'medium'
      },
      {
        id: 'regular-change',
        icon: '🔄',
        title: t('pit.security.tips.regular.title'),
        description: t('pit.security.tips.regular.description'),
        priority: 'medium'
      },
      {
        id: 'secure-environment',
        icon: '🏠',
        title: t('pit.security.tips.environment.title'),
        description: t('pit.security.tips.environment.description'),
        priority: 'medium'
      },
      {
        id: 'phishing-awareness',
        icon: '🎣',
        title: t('pit.security.tips.phishing.title'),
        description: t('pit.security.tips.phishing.description'),
        priority: 'medium'
      },
      {
        id: 'monitor-activity',
        icon: '👀',
        title: t('pit.security.tips.monitor.title'),
        description: t('pit.security.tips.monitor.description'),
        priority: 'low'
      },
      {
        id: 'backup-info',
        icon: '💾',
        title: t('pit.security.tips.backup.title'),
        description: t('pit.security.tips.backup.description'),
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
          {t('pit.security.tips.sectionCount', {
            title,
            count: tips.length,
            value: formatCoinNumber(tips.length, language),
          })}
        </h4>
        <div className={styles.tipsList}>
          {tips.map((tip) => (
            <div key={tip.id} className={`${styles.tipCard} ${styles[tip.priority]}`}>
              <div className={styles.tipIcon}>{tip.icon}</div>
              <div className={styles.tipContent}>
                <div className={styles.tipHeader}>
                  <h5 className={styles.tipTitle}>{tip.title}</h5>
                  <div className={`${styles.tipPriority} ${styles[tip.priority]}`}>
                    {t(`pit.security.tips.priority.${tip.priority}`)}
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
          {t('pit.security.tips.title')}
        </h3>
        <p className={styles.subtitle}>
          {t('pit.security.tips.description')}
        </p>
      </div>

      <div className={styles.content}>
        {renderTipSection(t('pit.security.tips.sectionUrgent'), highPriorityTips, 'high')}
        {renderTipSection(t('pit.security.tips.sectionImportant'), mediumPriorityTips, 'medium')}
        {renderTipSection(t('pit.security.tips.sectionGeneral'), lowPriorityTips, 'low')}

        {/* 安全知识 */}
        <div className={styles.knowledgeSection}>
          <h4 className={styles.knowledgeTitle}>
            <span className={styles.knowledgeIcon}>📚</span>
            {t('pit.security.knowledge.title')}
          </h4>
          <div className={styles.knowledgeCards}>
            <div className={styles.knowledgeCard}>
              <div className={styles.knowledgeCardIcon}>🔐</div>
              <div className={styles.knowledgeCardContent}>
                <h5>{t('pit.security.knowledge.passcodeTitle')}</h5>
                <p>{t('pit.security.knowledge.passcodeDescription')}</p>
              </div>
            </div>
            <div className={styles.knowledgeCard}>
              <div className={styles.knowledgeCardIcon}>🌐</div>
              <div className={styles.knowledgeCardContent}>
                <h5>{t('pit.security.knowledge.networkTitle')}</h5>
                <p>{t('pit.security.knowledge.networkDescription')}</p>
              </div>
            </div>
            <div className={styles.knowledgeCard}>
              <div className={styles.knowledgeCardIcon}>📱</div>
              <div className={styles.knowledgeCardContent}>
                <h5>{t('pit.security.knowledge.deviceTitle')}</h5>
                <p>{t('pit.security.knowledge.deviceDescription')}</p>
              </div>
            </div>
          </div>
        </div>

        {/* 联系支持 */}
        <div className={styles.supportSection}>
          <div className={styles.supportCard}>
            <div className={styles.supportIcon}>🆘</div>
            <div className={styles.supportContent}>
              <h4>{t('pit.security.support.title')}</h4>
              <p>{t('pit.security.support.description')}</p>
              <div className={styles.supportActions}>
                <button
                  type="button"
                  className={styles.supportButton}
                  onClick={() => navigateTo('password', 'support:password')}
                >
                  {t(status?.hasPaymentPassword
                    ? 'pit.security.passcode.changeFull'
                    : 'pit.security.passcode.setFull')}
                </button>
                <button
                  type="button"
                  className={styles.supportButton}
                  onClick={() => navigateTo('log', 'support:log')}
                >
                  {t('pit.security.action.logs')}
                </button>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};
