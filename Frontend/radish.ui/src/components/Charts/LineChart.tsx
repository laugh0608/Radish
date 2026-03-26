import {
  LineChart as RechartsLineChart,
  Line,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  Legend,
  ResponsiveContainer
} from 'recharts';
import { ChartContainer } from './ChartContainer';

export interface LineChartDataPoint {
  [key: string]: string | number;
}

export interface LineChartLine {
  dataKey: string;
  name: string;
  color: string;
  strokeWidth?: number;
}

export interface LineChartProps {
  data: LineChartDataPoint[];
  lines: LineChartLine[];
  xAxisKey: string;
  title?: string;
  loading?: boolean;
  error?: string | null;
  height?: number | string;
  showGrid?: boolean;
  showLegend?: boolean;
  className?: string;
}

/**
 * 通用折线图组件
 */
export const LineChart = ({
  data,
  lines,
  xAxisKey,
  title,
  loading = false,
  error = null,
  height = 300,
  showGrid = true,
  showLegend = true,
  className
}: LineChartProps) => {
  return (
    <ChartContainer
      title={title}
      loading={loading}
      error={error}
      height={height}
      className={className}
    >
      <ResponsiveContainer width="100%" height="100%">
        <RechartsLineChart data={data} margin={{ top: 5, right: 30, left: 20, bottom: 5 }}>
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
          {lines.map((line) => (
            <Line
              key={line.dataKey}
              type="monotone"
              dataKey={line.dataKey}
              name={line.name}
              stroke={line.color}
              strokeWidth={line.strokeWidth || 2}
              dot={{ fill: line.color, r: 4 }}
              activeDot={{ r: 6 }}
            />
          ))}
        </RechartsLineChart>
      </ResponsiveContainer>
    </ChartContainer>
  );
};
