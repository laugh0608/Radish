import { useUserStore } from '@/stores/userStore';
import { Icon } from '@radish/ui';
import styles from './WelcomeApp.module.css';

/**
 * 欢迎应用
 *
 * 展示用户信息和系统介绍
 */
export const WelcomeApp = () => {
  const { userName, userId, tenantId } = useUserStore();

  return (
    <div className={styles.container}>
      <div className={styles.hero}>
        <Icon icon="mdi:hand-wave" size={64} color="#667eea" />
        <h1 className={styles.title}>欢迎来到 Radish WebOS</h1>
        <p className={styles.subtitle}>
          你好，<strong>{userName}</strong>！
        </p>
      </div>

      <div className={styles.content}>
        <section className={styles.section}>
          <h2>
            <Icon icon="mdi:information" size={24} />
            关于 Radish WebOS
          </h2>
          <p>
            Radish WebOS 是一个运行在浏览器中的操作系统，提供类似桌面操作系统的体验。
            你可以双击桌面图标打开应用，拖拽窗口，就像使用真正的操作系统一样。
          </p>
        </section>

        <section className={styles.section}>
          <h2>
            <Icon icon="mdi:account-circle" size={24} />
            你的信息
          </h2>
          <ul className={styles.infoList}>
            <li>
              <span className={styles.label}>用户 ID：</span>
              <span className={styles.value}>{userId}</span>
            </li>
            <li>
              <span className={styles.label}>用户名：</span>
              <span className={styles.value}>{userName}</span>
            </li>
            <li>
              <span className={styles.label}>租户 ID：</span>
              <span className={styles.value}>{tenantId}</span>
            </li>
          </ul>
        </section>

        <section className={styles.section}>
          <h2>
            <Icon icon="mdi:lightbulb-on" size={24} />
            快速开始
          </h2>
          <ul className={styles.tipsList}>
            <li>
              <Icon icon="mdi:cursor-default-click" size={20} />
              <span>双击桌面图标打开应用</span>
            </li>
            <li>
              <Icon icon="mdi:drag" size={20} />
              <span>拖拽窗口标题栏可移动窗口</span>
            </li>
            <li>
              <Icon icon="mdi:close-circle" size={20} />
              <span>点击窗口右上角的红色按钮关闭窗口</span>
            </li>
            <li>
              <Icon icon="mdi:dock-bottom" size={20} />
              <span>底部 Dock 显示正在运行的应用</span>
            </li>
          </ul>
        </section>
      </div>
    </div>
  );
};
