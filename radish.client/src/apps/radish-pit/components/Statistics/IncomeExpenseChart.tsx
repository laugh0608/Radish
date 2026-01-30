import { BarChart } from '@radish/ui';
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
 * 收支图表组件 - 使用柱状图展示收入和支出
 */
export const IncomeExpenseChart = ({ data, loading, error, displayMode, timeRange }: IncomeExpenseChartProps) => {
  // 准备图表数据
  const chartData = data?.trendData.map((item, index) => ({
    name: item.date,
    收入: displayMode === 'carrot' ? item.income : item.income,
    支出: displayMode === 'carrot' ? item.expense : item.expense
  })) || [];

  // 根据显示模式设置颜色
  const incomeColor = displayMode === 'carrot' ? '#ff6b35' : '#4facfe';
  const expenseColor = displayMode === 'carrot' ? '#f7931e' : '#667eea';

  return (
    <div className={styles.container}>
      <BarChart
        data={chartData}
        bars={[
          { dataKey: '收入', name: '收入', color: incomeColor },
          { dataKey: '支出', name: '支出', color: expenseColor }
        ]}
        xAxisKey="name"
        title={`收支趋势 (${timeRange === 'month' ? '月度' : timeRange === 'quarter' ? '季度' : '年度'})`}
        loading={loading}
        error={error}
        height={350}
        showGrid={true}
        showLegend={true}
      />
    </div>
  );
};