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
          {showGrid && <CartesianGrid strokeDasharray="3 3" stroke="rgba(255, 255, 255, 0.1)" />}
          <XAxis
            dataKey={xAxisKey}
            stroke="rgba(255, 255, 255, 0.5)"
            style={{ fontSize: '12px' }}
          />
          <YAxis
            stroke="rgba(255, 255, 255, 0.5)"
            style={{ fontSize: '12px' }}
          />
          <Tooltip
            contentStyle={{
              backgroundColor: 'rgba(0, 0, 0, 0.8)',
              border: '1px solid rgba(255, 255, 255, 0.2)',
              borderRadius: '8px',
              color: '#fff'
            }}
          />
          {showLegend && (
            <Legend
              wrapperStyle={{
                color: 'rgba(255, 255, 255, 0.7)',
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
