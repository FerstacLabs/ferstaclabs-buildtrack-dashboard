import { Button } from 'antd'
import type { ReactNode } from 'react'

interface ToolbarButtonProps {
  children: ReactNode
  icon?: ReactNode
  onClick?: () => void
  tone?: 'primary' | 'green' | 'purple' | 'orange'
}

export const ToolbarButton = ({ children, icon, onClick, tone = 'primary' }: ToolbarButtonProps) => (
  <Button icon={icon} onClick={onClick} className={`toolbar-button toolbar-${tone}`}>
    {children}
  </Button>
)
