import { useEffect, useRef, useState, type ReactNode } from 'react';
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
  const chartWrapperRef = useRef<HTMLDivElement | null>(null);
  const [hasUsableSize, setHasUsableSize] = useState(false);

  useEffect(() => {
    const element = chartWrapperRef.current;
    if (!element) {
      return;
    }

    const updateSize = () => {
      const { width, height: wrapperHeight } = element.getBoundingClientRect();
      setHasUsableSize(width > 24 && wrapperHeight > 24);
    };

    updateSize();

    if (typeof ResizeObserver === 'undefined') {
      setHasUsableSize(true);
      return;
    }

    const observer = new ResizeObserver(() => {
      updateSize();
    });

    observer.observe(element);

    return () => {
      observer.disconnect();
    };
  }, [height]);

  const chartWrapperStyle = typeof height === 'number'
    ? { height: `${height}px`, minHeight: `${height}px` }
    : { height, minHeight: '220px' };

  return (
    <div className={`${styles.container} ${className}`}>
      {title && <h3 className={styles.title}>{title}</h3>}

      <div ref={chartWrapperRef} className={styles.chartWrapper} style={chartWrapperStyle}>
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

        {!loading && !error && hasUsableSize && (
          <div className={styles.chartCanvas}>
            {children}
          </div>
        )}

        {!loading && !error && !hasUsableSize && (
          <div className={styles.placeholder} aria-hidden="true" />
        )}
      </div>
    </div>
  );
};
