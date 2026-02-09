import {
  PieChart as RechartsPieChart,
  Pie,
  Cell,
  Tooltip,
  Legend,
  ResponsiveContainer,
  type PieLabelRenderProps
} from 'recharts';
import { ChartContainer } from './ChartContainer';

export interface PieChartDataPoint {
  name: string;
  value: number;
  color?: string;
  [key: string]: string | number | undefined;  // 添加索引签名以兼容 recharts
}

export interface PieChartProps {
  data: PieChartDataPoint[];
  title?: string;
  loading?: boolean;
  error?: string | null;
  height?: number | string;
  showLegend?: boolean;
  innerRadius?: number;
  outerRadius?: number;
  showLabel?: boolean;
  className?: string;
}

const DEFAULT_COLORS = [
  '#667eea',
  '#764ba2',
  '#f093fb',
  '#4facfe',
  '#43e97b',
  '#fa709a',
  '#fee140',
  '#30cfd0'
];

/**
 * 通用饼图组件
 */
export const PieChart = ({
  data,
  title,
  loading = false,
  error = null,
  height = 300,
  showLegend = true,
  innerRadius = 0,
  outerRadius = 80,
  showLabel = true,
  className
}: PieChartProps) => {
  const renderLabel = (props: PieLabelRenderProps) => {
    const { name, value } = props;
    if (typeof value !== 'number' || !name) return '';
    const total = data.reduce((sum, item) => sum + item.value, 0);
    const percent = ((value / total) * 100).toFixed(1);
    return `${name}: ${percent}%`;
  };

  return (
    <ChartContainer
      title={title}
      loading={loading}
      error={error}
      height={height}
      className={className}
    >
      <ResponsiveContainer width="100%" height="100%">
        <RechartsPieChart>
          <Pie
            data={data}
            cx="50%"
            cy="50%"
            innerRadius={innerRadius}
            outerRadius={outerRadius}
            fill="#8884d8"
            dataKey="value"
            label={showLabel ? renderLabel : false}
            labelLine={showLabel}
          >
            {data.map((entry, index) => (
              <Cell
                key={`cell-${index}`}
                fill={entry.color || DEFAULT_COLORS[index % DEFAULT_COLORS.length]}
              />
            ))}
          </Pie>
          <Tooltip
            contentStyle={{
              backgroundColor: '#ffffff',
              border: '1px solid #e5e7eb',
              borderRadius: '8px',
              color: '#1f2937',
              boxShadow: '0 4px 6px rgba(0, 0, 0, 0.1)'
            }}
          />
          {showLegend && (
            <Legend
              wrapperStyle={{
                color: '#1f2937',
                fontSize: '12px'
              }}
            />
          )}
        </RechartsPieChart>
      </ResponsiveContainer>
    </ChartContainer>
  );
};
