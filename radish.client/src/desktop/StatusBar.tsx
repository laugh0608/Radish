import { useState, useEffect } from 'react';
import { useUserStore } from '@/stores/userStore';
import styles from './StatusBar.module.css';

/**
 * 状态栏组件
 *
 * 显示系统信息、用户信息和时间
 */
export const StatusBar = () => {
  const { userName, userId } = useUserStore();
  const [time, setTime] = useState(new Date());

  useEffect(() => {
    const timer = setInterval(() => {
      setTime(new Date());
    }, 1000);

    return () => clearInterval(timer);
  }, []);

  return (
    <div className={styles.statusBar}>
      <div className={styles.left}>
        <span className={styles.brand}>Radish OS</span>
        {userName && (
          <span className={styles.user}>
            👤 {userName} (ID: {userId})
          </span>
        )}
      </div>
      <div className={styles.right}>
        <span className={styles.time}>
          {time.toLocaleTimeString('zh-CN', { hour: '2-digit', minute: '2-digit', second: '2-digit' })}
        </span>
      </div>
    </div>
  );
};
