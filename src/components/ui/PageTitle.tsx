import type { ReactNode } from 'react'
import { HeaderBar } from '../layout/HeaderBar'

interface PageTitleProps {
  title: string
  subtitle?: string
  extra?: ReactNode
}

export const PageTitle = ({ title, subtitle, extra }: PageTitleProps) => (
  <header className="page-title-row">
    <div>
      <h1>{title}</h1>
      {subtitle ? <p>{subtitle}</p> : null}
    </div>
    <div className="page-title-extra">
      {extra}
      <HeaderBar />
    </div>
  </header>
)
