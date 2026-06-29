import {
  Bar,
  BarChart,
  CartesianGrid,
  Legend,
  ResponsiveContainer,
  Tooltip as ChartTooltip,
  XAxis,
  YAxis,
} from 'recharts'
import type { ChartPoint } from '../../types/reports'

interface BarChartCardProps {
  title: string
  data: ChartPoint[]
  bars: Array<{ dataKey: keyof ChartPoint; color: string; name: string }>
  height?: number
}

export const BarChartCard = ({ bars, data, height = 280, title }: BarChartCardProps) => (
  <section className="chart-card">
    <h2>{title}</h2>
    <ResponsiveContainer width="100%" height={height}>
      <BarChart data={data}>
        <CartesianGrid stroke="#e8edf5" vertical={false} />
        <XAxis dataKey="name" tick={{ fill: '#071b55', fontSize: 12 }} />
        <YAxis tick={{ fill: '#50607f', fontSize: 12 }} />
        <ChartTooltip />
        <Legend />
        {bars.map((bar) => (
          <Bar key={String(bar.dataKey)} dataKey={bar.dataKey} fill={bar.color} name={bar.name} radius={[6, 6, 0, 0]} />
        ))}
      </BarChart>
    </ResponsiveContainer>
  </section>
)
