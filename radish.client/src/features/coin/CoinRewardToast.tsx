import { toast } from '@radish/ui';
import styles from './CoinRewardToast.module.css';

export interface CoinRewardInfo {
  /** 奖励金额（胡萝卜） */
  amount: number;
  /** 奖励类型 */
  type: 'LIKE_REWARD' | 'COMMENT_REWARD' | 'HIGHLIGHT_REWARD' | 'SYSTEM_GRANT' | 'TRANSFER' | 'ADMIN_ADJUST';
  /** 奖励原因描述 */
  reason?: string;
}

const REWARD_CONFIG = {
  LIKE_REWARD: {
    icon: '👍',
    text: '点赞奖励'
  },
  COMMENT_REWARD: {
    icon: '💬',
    text: '评论奖励'
  },
  HIGHLIGHT_REWARD: {
    icon: '🌟',
    text: '神评/沙发奖励'
  },
  SYSTEM_GRANT: {
    icon: '🎁',
    text: '系统赠送'
  },
  TRANSFER: {
    icon: '💰',
    text: '转账收入'
  },
  ADMIN_ADJUST: {
    icon: '⚙️',
    text: '管理员调整'
  }
};

/**
 * 显示萝卜币奖励通知
 */
export const showCoinReward = (reward: CoinRewardInfo) => {
  const config = REWARD_CONFIG[reward.type];
  const radishAmount = (reward.amount / 1000).toFixed(3);

  const message = (
    <div className={styles.rewardContent}>
      <div className={styles.rewardHeader}>
        <span className={styles.rewardIcon}>{config.icon}</span>
        <span className={styles.rewardType}>{config.text}</span>
      </div>
      <div className={styles.rewardAmount}>
        <span className={styles.carrotIcon}>🥕</span>
        <span className={styles.amount}>+{reward.amount.toLocaleString()}</span>
        <span className={styles.unit}>胡萝卜</span>
      </div>
      <div className={styles.rewardAmountAlt}>
        ({radishAmount} 白萝卜)
      </div>
      {reward.reason && (
        <div className={styles.rewardReason}>
          {reward.reason}
        </div>
      )}
    </div>
  );

  // 使用自定义图标和较长的显示时间
  toast.custom({
    message: message,
    type: 'success',
    duration: 5000,
    icon: <span className={styles.customIcon}>🥕</span>
  });
};

/**
 * 批量显示萝卜币奖励（避免通知过多）
 */
export const showBatchCoinReward = (rewards: CoinRewardInfo[]) => {
  if (rewards.length === 0) return;

  if (rewards.length === 1) {
    showCoinReward(rewards[0]);
    return;
  }

  const totalAmount = rewards.reduce((sum, r) => sum + r.amount, 0);
  const radishAmount = (totalAmount / 1000).toFixed(3);

  const message = (
    <div className={styles.rewardContent}>
      <div className={styles.rewardHeader}>
        <span className={styles.rewardIcon}>🎉</span>
        <span className={styles.rewardType}>获得多项奖励</span>
      </div>
      <div className={styles.rewardAmount}>
        <span className={styles.carrotIcon}>🥕</span>
        <span className={styles.amount}>+{totalAmount.toLocaleString()}</span>
        <span className={styles.unit}>胡萝卜</span>
      </div>
      <div className={styles.rewardAmountAlt}>
        ({radishAmount} 白萝卜)
      </div>
      <div className={styles.rewardList}>
        {rewards.map((r, i) => {
          const config = REWARD_CONFIG[r.type];
          return (
            <div key={i} className={styles.rewardItem}>
              {config.icon} {config.text} +{r.amount}
            </div>
          );
        })}
      </div>
    </div>
  );

  toast.custom({
    message: message,
    type: 'success',
    duration: 6000,
    icon: <span className={styles.customIcon}>🥕</span>
  });
};
