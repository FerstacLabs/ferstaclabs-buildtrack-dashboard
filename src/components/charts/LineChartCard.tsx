import {
  CartesianGrid,
  Legend,
  Line,
  LineChart,
  ResponsiveContainer,
  Tooltip as ChartTooltip,
  XAxis,
  YAxis,
} from 'recharts'
import type { ChartPoint } from '../../types/reports'
import { WrappedAxisTick } from './WrappedAxisTick'

interface LineChartCardProps {
  title: string
  data: ChartPoint[]
  lines: Array<{ dataKey: keyof ChartPoint; color: string; name: string }>
  height?: number
}

export const LineChartCard = ({ data, height = 280, lines, title }: LineChartCardProps) => (
  <section className="chart-card">
    <h2>{title}</h2>
    <ResponsiveContainer width="100%" height={height}>
      <LineChart data={data} margin={{ top: 12, right: 12, left: 0, bottom: 24 }}>
        <CartesianGrid stroke="#e8edf5" vertical={false} />
        <XAxis
          dataKey="name"
          height={62}
          interval={0}
          tick={<WrappedAxisTick maxCharsPerLine={12} maxLines={2} />}
          tickLine={false}
          tickMargin={10}
        />
        <YAxis tick={{ fill: '#50607f', fontSize: 12 }} />
        <ChartTooltip />
        <Legend />
        {lines.map((line) => (
          <Line
            key={String(line.dataKey)}
            type="monotone"
            dataKey={line.dataKey}
            stroke={line.color}
            strokeWidth={3}
            dot={{ r: 4 }}
            name={line.name}
          />
        ))}
      </LineChart>
    </ResponsiveContainer>
  </section>
)
