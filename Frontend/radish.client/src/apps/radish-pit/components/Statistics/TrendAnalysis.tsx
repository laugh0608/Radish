import { AreaChart } from '@radish/ui/area-chart';
import { useTranslation } from 'react-i18next';
import { formatCoinAmount, formatCoinChartDate, toCoinChartNumber } from '../../utils';
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
  const { t, i18n } = useTranslation();
  const language = i18n.resolvedLanguage ?? i18n.language;
  // 准备图表数据（直接使用 vo 前缀字段）
  const chartData = data?.voTrendData.map((item) => ({
    name: formatCoinChartDate(item.voDate, language),
    income: toCoinChartNumber(item.voIncome),
    expense: toCoinChartNumber(item.voExpense),
    net: toCoinChartNumber(item.voIncome) - toCoinChartNumber(item.voExpense),
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
          { dataKey: 'income', name: t('pit.statistics.income'), color: incomeColor, fillOpacity: 0.6 },
          { dataKey: 'expense', name: t('pit.statistics.expense'), color: expenseColor, fillOpacity: 0.6 },
          { dataKey: 'net', name: t('pit.statistics.netIncome'), color: netColor, fillOpacity: 0.4 }
        ]}
        xAxisKey="name"
        title={t('pit.statistics.trendTitle', { range: t(`pit.statistics.range.${timeRange}`) })}
        loading={loading}
        error={error}
        height={350}
        showGrid={true}
        showLegend={true}
        stacked={false}
        valueFormatter={(value) => formatCoinAmount(value, language, t, displayMode)}
      />
    </div>
  );
};
