import { Cell, Pie, PieChart, ResponsiveContainer, Tooltip as ChartTooltip } from 'recharts'
import type { ChartPoint } from '../../types/reports'

interface DonutChartCardProps {
  title: string
  data: ChartPoint[]
  centerLabel?: string
  centerValue?: string
  height?: number
}

const colors = ['#078b55', '#1479ff', '#ff9700', '#ef233c', '#7546c9', '#10b7a6']

export const DonutChartCard = ({ centerLabel, centerValue, data, height = 260, title }: DonutChartCardProps) => (
  <section className="chart-card donut-card">
    <h2>{title}</h2>
    <div className="donut-wrap">
      <ResponsiveContainer width="55%" height={height}>
        <PieChart>
          <Pie data={data} innerRadius="58%" outerRadius="82%" paddingAngle={1} dataKey="value">
            {data.map((entry, index) => (
              <Cell key={entry.name} fill={colors[index % colors.length]} />
            ))}
          </Pie>
          <ChartTooltip />
        </PieChart>
      </ResponsiveContainer>
      <div className="donut-center">
        <strong>{centerValue}</strong>
        <span>{centerLabel}</span>
      </div>
      <div className="donut-legend">
        {data.map((item, index) => (
          <div key={item.name}>
            <i style={{ background: colors[index % colors.length] }} />
            <span>{item.name}</span>
            <strong>{item.value}</strong>
          </div>
        ))}
      </div>
    </div>
  </section>
)
