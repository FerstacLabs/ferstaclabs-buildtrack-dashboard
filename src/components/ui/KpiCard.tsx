import type { ReactNode } from 'react'

interface KpiCardProps {
  title: string
  value: string
  suffix?: string
  trend?: string
  tone: 'blue' | 'green' | 'orange' | 'red' | 'purple'
  icon: ReactNode
}

export const KpiCard = ({ icon, suffix, title, tone, trend, value }: KpiCardProps) => (
  <div className={`kpi-card kpi-${tone}`}>
    <div className="kpi-top">
      <span className="kpi-icon">{icon}</span>
      <span className="kpi-title">{title}</span>
    </div>
    <div className="kpi-value">
      {value}
      {suffix ? <small>{suffix}</small> : null}
    </div>
    {trend ? <div className="kpi-trend">↑ {trend}</div> : null}
  </div>
)
