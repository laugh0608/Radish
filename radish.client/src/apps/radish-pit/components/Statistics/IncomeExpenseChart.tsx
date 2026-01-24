import type { StatisticsData } from '../../types';
import styles from './IncomeExpenseChart.module.css';

interface IncomeExpenseChartProps {
  data: StatisticsData | null;
  loading: boolean;
  error: string | null;
  displayMode: 'carrot' | 'white';
  timeRange: 'month' | 'quarter' | 'year';
}

/**
 * 收支图表组件
 */
export const IncomeExpenseChart = ({ data, loading, error }: IncomeExpenseChartProps) => {
  if (loading) {
    return (
      <div className={styles.container}>
        <div className={styles.loading}>
          <div className={styles.loadingSpinner}></div>
          <p>加载图表数据中...</p>
        </div>
      </div>
    );
  }

  return (
    <div className={styles.container}>
      <div className={styles.placeholder}>
        <div className={styles.placeholderIcon}>📈</div>
        <h3>收支图表</h3>
        <p>图表功能开发中，敬请期待</p>
      </div>
    </div>
  );
};