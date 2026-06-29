import type { ReactNode } from 'react'

interface ExplanationCardProps {
  title: string
  icon: ReactNode
  tone?: 'green' | 'blue' | 'orange' | 'purple' | 'red'
  children: ReactNode
}

export const ExplanationCard = ({ children, icon, title, tone = 'green' }: ExplanationCardProps) => (
  <section className={`explanation-card explanation-${tone}`}>
    <div className="explanation-icon">{icon}</div>
    <div>
      <h3>{title}</h3>
      <div className="explanation-body">{children}</div>
    </div>
  </section>
)
