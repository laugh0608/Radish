import { BarChart } from '@radish/ui/bar-chart';
import { useTranslation } from 'react-i18next';
import { formatCoinAmount, formatCoinChartDate, toCoinChartNumber } from '../../utils';
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
  const { t, i18n } = useTranslation();
  const language = i18n.resolvedLanguage ?? i18n.language;
  // 准备图表数据
  const chartData = data?.voTrendData.map((item) => ({
    name: formatCoinChartDate(item.voDate, language),
    income: toCoinChartNumber(item.voIncome),
    expense: toCoinChartNumber(item.voExpense),
  })) || [];

  // 根据显示模式设置颜色
  const incomeColor = displayMode === 'carrot' ? '#ff6b35' : '#4facfe';
  const expenseColor = displayMode === 'carrot' ? '#f7931e' : '#667eea';

  return (
    <div className={styles.container}>
      <BarChart
        data={chartData}
        bars={[
          { dataKey: 'income', name: t('pit.statistics.income'), color: incomeColor },
          { dataKey: 'expense', name: t('pit.statistics.expense'), color: expenseColor }
        ]}
        xAxisKey="name"
        title={t('pit.statistics.chartTitle', { range: t(`pit.statistics.range.${timeRange}`) })}
        loading={loading}
        error={error}
        height={350}
        showGrid={true}
        showLegend={true}
        valueFormatter={(value) => formatCoinAmount(value, language, t, displayMode)}
      />
    </div>
  );
};
