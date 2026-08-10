import { Tag } from 'antd'
import { fieldStatusColor, fieldStatusLabel } from './fieldPortalStore'

export const FieldStatusTag = ({ status }: { status?: string }) => {
  const canonicalStatus = status?.trim()
  const statusKey = canonicalStatus || 'unknown'
  const label = fieldStatusLabel(canonicalStatus)

  return (
    <Tag key={statusKey} color={fieldStatusColor(canonicalStatus)} data-status={canonicalStatus ?? ''}>
      <span key={`status-text:${statusKey}`}>
        {label}
      </span>
    </Tag>
  )
}
