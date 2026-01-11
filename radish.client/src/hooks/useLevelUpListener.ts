import { useEffect, useState } from 'react';
import { notificationHub } from '@/services/notificationHub';
import type { LevelUpData } from '@radish/ui';

/**
 * 升级事件监听 Hook
 *
 * 监听 SignalR 推送的升级事件，并触发升级动画弹窗
 */
export const useLevelUpListener = () => {
  const [levelUpData, setLevelUpData] = useState<LevelUpData | null>(null);
  const [showModal, setShowModal] = useState(false);

  useEffect(() => {
    // 监听升级事件
    const handleLevelUp = (data: any) => {
      console.log('收到升级事件:', data);

      // 转换为 LevelUpData 格式
      const levelUpInfo: LevelUpData = {
        oldLevel: data.oldLevel || 0,
        newLevel: data.newLevel || 0,
        oldLevelName: data.oldLevelName || '凡人',
        newLevelName: data.newLevelName || '练气',
        themeColor: data.themeColor || '#9E9E9E',
        rewards: {
          coins: data.coinReward || 0,
          items: data.items || []
        },
        message: data.message || `恭喜你升级到 ${data.newLevelName}！`
      };

      setLevelUpData(levelUpInfo);
      setShowModal(true);
    };

    // 获取底层 SignalR 连接并注册事件监听
    const connection = notificationHub.getConnection();
    if (connection) {
      connection.on('LevelUp', handleLevelUp);
    }

    // 清理函数
    return () => {
      const connection = notificationHub.getConnection();
      if (connection) {
        connection.off('LevelUp', handleLevelUp);
      }
    };
  }, []);

  const handleClose = () => {
    setShowModal(false);
    // 延迟清空数据，等待动画完成
    setTimeout(() => {
      setLevelUpData(null);
    }, 300);
  };

  return {
    levelUpData,
    showModal,
    handleClose
  };
};
