import type { StatisticsData } from '../../types';
import styles from './TrendAnalysis.module.css';

interface TrendAnalysisProps {
  data: StatisticsData | null;
  loading: boolean;
  error: string | null;
  displayMode: 'carrot' | 'white';
  timeRange: 'month' | 'quarter' | 'year';
}

/**
 * 趋势分析组件
 */
export const TrendAnalysis = ({ data, loading, error }: TrendAnalysisProps) => {
  if (loading) {
    return (
      <div className={styles.container}>
        <div className={styles.loading}>
          <div className={styles.loadingSpinner}></div>
          <p>加载趋势数据中...</p>
        </div>
      </div>
    );
  }

  return (
    <div className={styles.container}>
      <div className={styles.placeholder}>
        <div className={styles.placeholderIcon}>📉</div>
        <h3>趋势分析</h3>
        <p>趋势分析功能开发中，敬请期待</p>
      </div>
    </div>
  );
};