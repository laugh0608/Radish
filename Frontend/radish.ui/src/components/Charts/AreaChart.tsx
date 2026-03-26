import {
  AreaChart as RechartsAreaChart,
  Area,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  Legend,
  ResponsiveContainer
} from 'recharts';
import { ChartContainer } from './ChartContainer';

export interface AreaChartDataPoint {
  [key: string]: string | number;
}

export interface AreaChartArea {
  dataKey: string;
  name: string;
  color: string;
  fillOpacity?: number;
}

export interface AreaChartProps {
  data: AreaChartDataPoint[];
  areas: AreaChartArea[];
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
 * 通用面积图组件
 */
export const AreaChart = ({
  data,
  areas,
  xAxisKey,
  title,
  loading = false,
  error = null,
  height = 300,
  showGrid = true,
  showLegend = true,
  stacked = false,
  className
}: AreaChartProps) => {
  return (
    <ChartContainer
      title={title}
      loading={loading}
      error={error}
      height={height}
      className={className}
    >
      <ResponsiveContainer width="100%" height="100%">
        <RechartsAreaChart data={data} margin={{ top: 5, right: 30, left: 20, bottom: 5 }}>
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
          {areas.map((area) => (
            <Area
              key={area.dataKey}
              type="monotone"
              dataKey={area.dataKey}
              name={area.name}
              stroke={area.color}
              fill={area.color}
              fillOpacity={area.fillOpacity || 0.6}
              stackId={stacked ? 'stack' : undefined}
            />
          ))}
        </RechartsAreaChart>
      </ResponsiveContainer>
    </ChartContainer>
  );
};
