import { AreaChart } from '@radish/ui';
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
 * 趋势分析组件 - 使用面积图展示收支趋势
 */
export const TrendAnalysis = ({ data, loading, error, displayMode, timeRange }: TrendAnalysisProps) => {
  // 准备图表数据
  const chartData = data?.trendData.map((item) => ({
    name: item.date,
    收入: item.income,
    支出: item.expense,
    净收入: item.income - item.expense
  })) || [];

  // 根据显示模式设置颜色
  const incomeColor = displayMode === 'carrot' ? '#ff6b35' : '#4facfe';
  const expenseColor = displayMode === 'carrot' ? '#f7931e' : '#667eea';
  const netColor = displayMode === 'carrot' ? '#43e97b' : '#30cfd0';

  return (
    <div className={styles.container}>
      <AreaChart
        data={chartData}
        areas={[
          { dataKey: '收入', name: '收入', color: incomeColor, fillOpacity: 0.6 },
          { dataKey: '支出', name: '支出', color: expenseColor, fillOpacity: 0.6 },
          { dataKey: '净收入', name: '净收入', color: netColor, fillOpacity: 0.4 }
        ]}
        xAxisKey="name"
        title={`趋势分析 (${timeRange === 'month' ? '月度' : timeRange === 'quarter' ? '季度' : '年度'})`}
        loading={loading}
        error={error}
        height={350}
        showGrid={true}
        showLegend={true}
        stacked={false}
      />
    </div>
  );
};