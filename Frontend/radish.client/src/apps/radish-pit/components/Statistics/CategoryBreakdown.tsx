import { PieChart } from '@radish/ui/pie-chart';
import { useTranslation } from 'react-i18next';
import { formatCoinAmount, formatStatisticsCategory, toCoinChartNumber } from '../../utils';
import type { StatisticsData } from '../../types';
import styles from './CategoryBreakdown.module.css';

interface CategoryBreakdownProps {
  data: StatisticsData | null;
  loading: boolean;
  error: string | null;
  displayMode: 'carrot' | 'white';
}

// 分类颜色映射
const CATEGORY_COLORS = ['#667eea', '#764ba2', '#f093fb', '#4facfe', '#43e97b'];

/**
 * 分类统计组件 - 使用饼图展示分类占比
 */
export const CategoryBreakdown = ({ data, loading, error, displayMode }: CategoryBreakdownProps) => {
  const { t, i18n } = useTranslation();
  const language = i18n.resolvedLanguage ?? i18n.language;
  // 准备图表数据（直接使用 vo 前缀字段）
  const chartData = data?.voCategoryStats.map((item, index) => ({
    name: formatStatisticsCategory(item.voCategory, t),
    value: toCoinChartNumber(item.voAmount),
    color: CATEGORY_COLORS[index % CATEGORY_COLORS.length],
  })) || [];

  return (
    <div className={styles.container}>
      <PieChart
        data={chartData}
        title={t('pit.statistics.categoryTitle')}
        loading={loading}
        error={error}
        height={350}
        showLegend={true}
        innerRadius={60}
        outerRadius={100}
        showLabel={true}
        valueFormatter={(value) => formatCoinAmount(value, language, t, displayMode)}
      />
    </div>
  );
};
