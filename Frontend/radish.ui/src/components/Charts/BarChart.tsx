import {
  BarChart as RechartsBarChart,
  Bar,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  Legend,
  ResponsiveContainer
} from 'recharts';
import { ChartContainer } from './ChartContainer';

export interface BarChartDataPoint {
  [key: string]: string | number;
}

export interface BarChartBar {
  dataKey: string;
  name: string;
  color: string;
}

export interface BarChartProps {
  data: BarChartDataPoint[];
  bars: BarChartBar[];
  xAxisKey: string;
  title?: string;
  loading?: boolean;
  error?: string | null;
  height?: number | string;
  showGrid?: boolean;
  showLegend?: boolean;
  stacked?: boolean;
  className?: string;
}

/**
 * 通用柱状图组件
 */
export const BarChart = ({
  data,
  bars,
  xAxisKey,
  title,
  loading = false,
  error = null,
  height = 300,
  showGrid = true,
  showLegend = true,
  stacked = false,
  className
}: BarChartProps) => {
  return (
    <ChartContainer
      title={title}
      loading={loading}
      error={error}
      height={height}
      className={className}
    >
      <ResponsiveContainer width="100%" height="100%">
        <RechartsBarChart data={data} margin={{ top: 5, right: 30, left: 20, bottom: 5 }}>
          {showGrid && <CartesianGrid strokeDasharray="3 3" stroke="var(--theme-border-soft, rgba(84, 108, 122, 0.16))" />}
          <XAxis
            dataKey={xAxisKey}
            stroke="var(--theme-text-secondary, #5d6b75)"
            style={{ fontSize: '12px' }}
          />
          <YAxis
            stroke="var(--theme-text-secondary, #5d6b75)"
            style={{ fontSize: '12px' }}
          />
          <Tooltip
            contentStyle={{
              backgroundColor: 'var(--theme-bg-surface, #fbfcfc)',
              border: '1px solid var(--theme-border-soft, rgba(84, 108, 122, 0.16))',
              borderRadius: '12px',
              color: 'var(--theme-text-primary, #23313b)',
              boxShadow: 'var(--theme-shadow-soft, 0 16px 40px rgba(51, 72, 84, 0.12))'
            }}
          />
          {showLegend && (
            <Legend
              wrapperStyle={{
                color: 'var(--theme-text-primary, #23313b)',
                fontSize: '12px'
              }}
            />
          )}
          {bars.map((bar) => (
            <Bar
              key={bar.dataKey}
              dataKey={bar.dataKey}
              name={bar.name}
              fill={bar.color}
              stackId={stacked ? 'stack' : undefined}
              radius={[8, 8, 0, 0]}
            />
          ))}
        </RechartsBarChart>
      </ResponsiveContainer>
    </ChartContainer>
  );
};
