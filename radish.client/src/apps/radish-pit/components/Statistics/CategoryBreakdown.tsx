import type { StatisticsData } from '../../types';
import styles from './CategoryBreakdown.module.css';

interface CategoryBreakdownProps {
  data: StatisticsData | null;
  loading: boolean;
  error: string | null;
  displayMode: 'carrot' | 'white';
}

/**
 * 分类统计组件
 */
export const CategoryBreakdown = ({ data, loading, error }: CategoryBreakdownProps) => {
  if (loading) {
    return (
      <div className={styles.container}>
        <div className={styles.loading}>
          <div className={styles.loadingSpinner}></div>
          <p>加载分类数据中...</p>
        </div>
      </div>
    );
  }

  return (
    <div className={styles.container}>
      <div className={styles.placeholder}>
        <div className={styles.placeholderIcon}>🏷️</div>
        <h3>分类统计</h3>
        <p>分类统计功能开发中，敬请期待</p>
      </div>
    </div>
  );
};