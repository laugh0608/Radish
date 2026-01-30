import { ReactNode } from 'react';
import styles from './ChartContainer.module.css';

export interface ChartContainerProps {
  title?: string;
  loading?: boolean;
  error?: string | null;
  children: ReactNode;
  height?: number | string;
  className?: string;
}

/**
 * 图表容器组件 - 提供统一的加载、错误状态和布局
 */
export const ChartContainer = ({
  title,
  loading,
  error,
  children,
  height = 300,
  className = ''
}: ChartContainerProps) => {
  return (
    <div className={`${styles.container} ${className}`}>
      {title && <h3 className={styles.title}>{title}</h3>}

      <div className={styles.chartWrapper} style={{ height }}>
        {loading && (
          <div className={styles.loading}>
            <div className={styles.spinner}></div>
            <p>加载中...</p>
          </div>
        )}

        {error && (
          <div className={styles.error}>
            <div className={styles.errorIcon}>⚠️</div>
            <p>{error}</p>
          </div>
        )}

        {!loading && !error && children}
      </div>
    </div>
  );
};
