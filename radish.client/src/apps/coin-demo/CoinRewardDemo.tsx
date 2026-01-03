import { useState } from 'react';
import { showCoinReward, showBatchCoinReward, type CoinRewardInfo } from '@/features/coin';
import { toast } from '@radish/ui';
import styles from './CoinRewardDemo.module.css';

/**
 * 萝卜币奖励通知演示组件
 *
 * 用于测试和演示各种萝卜币奖励通知
 */
export const CoinRewardDemo = () => {
  const [customAmount, setCustomAmount] = useState(50);

  const handleLikeReward = () => {
    showCoinReward({
      amount: 10,
      type: 'LIKE_REWARD',
      reason: '你的帖子获得了点赞'
    });
  };

  const handleCommentReward = () => {
    showCoinReward({
      amount: 20,
      type: 'COMMENT_REWARD',
      reason: '你发表了评论'
    });
  };

  const handleHighlightReward = () => {
    showCoinReward({
      amount: 100,
      type: 'HIGHLIGHT_REWARD',
      reason: '你的评论被评为神评！'
    });
  };

  const handleSystemGrant = () => {
    showCoinReward({
      amount: 50,
      type: 'SYSTEM_GRANT',
      reason: '新用户注册奖励'
    });
  };

  const handleTransfer = () => {
    showCoinReward({
      amount: 500,
      type: 'TRANSFER',
      reason: '来自用户 @Alice 的转账'
    });
  };

  const handleAdminAdjust = () => {
    showCoinReward({
      amount: customAmount,
      type: 'ADMIN_ADJUST',
      reason: '管理员调整余额'
    });
  };

  const handleBatchReward = () => {
    const rewards: CoinRewardInfo[] = [
      { amount: 10, type: 'LIKE_REWARD' },
      { amount: 20, type: 'COMMENT_REWARD' },
      { amount: 10, type: 'LIKE_REWARD' },
      { amount: 100, type: 'HIGHLIGHT_REWARD' }
    ];
    showBatchCoinReward(rewards);
  };

  const handleMultipleToasts = () => {
    toast.success('这是一个成功消息');
    setTimeout(() => {
      toast.info('这是一个信息提示');
    }, 500);
    setTimeout(() => {
      handleLikeReward();
    }, 1000);
    setTimeout(() => {
      toast.warning('这是一个警告消息');
    }, 1500);
  };

  return (
    <div className={styles.container}>
      <div className={styles.header}>
        <h2 className={styles.title}>🥕 萝卜币奖励通知演示</h2>
        <p className={styles.description}>
          点击下面的按钮测试不同类型的萝卜币奖励通知效果
        </p>
      </div>

      <div className={styles.section}>
        <h3 className={styles.sectionTitle}>单项奖励</h3>
        <div className={styles.buttonGrid}>
          <button className={styles.button} onClick={handleLikeReward}>
            👍 点赞奖励 (+10)
          </button>
          <button className={styles.button} onClick={handleCommentReward}>
            💬 评论奖励 (+20)
          </button>
          <button className={styles.button} onClick={handleHighlightReward}>
            🌟 神评奖励 (+100)
          </button>
          <button className={styles.button} onClick={handleSystemGrant}>
            🎁 系统赠送 (+50)
          </button>
          <button className={styles.button} onClick={handleTransfer}>
            💰 转账收入 (+500)
          </button>
        </div>
      </div>

      <div className={styles.section}>
        <h3 className={styles.sectionTitle}>自定义奖励</h3>
        <div className={styles.customReward}>
          <input
            type="number"
            value={customAmount}
            onChange={(e) => setCustomAmount(Number(e.target.value))}
            className={styles.input}
            min="1"
            max="10000"
          />
          <button className={styles.button} onClick={handleAdminAdjust}>
            ⚙️ 管理员调整 (+{customAmount})
          </button>
        </div>
      </div>

      <div className={styles.section}>
        <h3 className={styles.sectionTitle}>批量奖励</h3>
        <div className={styles.buttonGrid}>
          <button className={styles.button} onClick={handleBatchReward}>
            🎉 批量奖励 (4项，共 140)
          </button>
          <button className={styles.button} onClick={handleMultipleToasts}>
            📢 多个通知叠加
          </button>
        </div>
      </div>

      <div className={styles.section}>
        <h3 className={styles.sectionTitle}>普通 Toast 通知</h3>
        <div className={styles.buttonGrid}>
          <button className={styles.button} onClick={() => toast.success('操作成功！')}>
            ✓ 成功通知
          </button>
          <button className={styles.button} onClick={() => toast.error('操作失败！')}>
            ✕ 错误通知
          </button>
          <button className={styles.button} onClick={() => toast.info('这是一条信息')}>
            ℹ 信息通知
          </button>
          <button className={styles.button} onClick={() => toast.warning('请注意')}>
            ⚠ 警告通知
          </button>
        </div>
      </div>

      <div className={styles.tips}>
        <h4 className={styles.tipsTitle}>💡 使用说明</h4>
        <ul className={styles.tipsList}>
          <li>单项奖励会显示具体的奖励类型和金额</li>
          <li>批量奖励会合并显示多个奖励的总和</li>
          <li>通知会自动在 3-6 秒后消失</li>
          <li>支持同时显示多个通知，不会相互覆盖</li>
        </ul>
      </div>
    </div>
  );
};
