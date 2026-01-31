import { PieChart } from '@radish/ui';
import type { StatisticsData } from '../../types';
import styles from './CategoryBreakdown.module.css';

interface CategoryBreakdownProps {
  data: StatisticsData | null;
  loading: boolean;
  error: string | null;
  displayMode: 'carrot' | 'white';
}

// 分类颜色映射
const CATEGORY_COLORS: Record<string, string> = {
  '转账': '#667eea',
  '购物': '#764ba2',
  '奖励': '#f093fb',
  '系统': '#4facfe',
  '其他': '#43e97b'
};

/**
 * 分类统计组件 - 使用饼图展示分类占比
 */
export const CategoryBreakdown = ({ data, loading, error, displayMode }: CategoryBreakdownProps) => {
  // 准备图表数据（直接使用 vo 前缀字段）
  const chartData = data?.voCategoryStats.map((item) => ({
    name: item.voCategory,
    value: item.voAmount,
    color: CATEGORY_COLORS[item.voCategory] || '#fa709a'
  })) || [];

  return (
    <div className={styles.container}>
      <PieChart
        data={chartData}
        title="分类统计"
        loading={loading}
        error={error}
        height={350}
        showLegend={true}
        innerRadius={60}
        outerRadius={100}
        showLabel={true}
      />
    </div>
  );
};