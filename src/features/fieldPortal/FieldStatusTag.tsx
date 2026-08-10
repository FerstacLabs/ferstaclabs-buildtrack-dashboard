import { Tag } from 'antd'
import { fieldStatusColor, fieldStatusLabel } from './fieldPortalStore'

export const FieldStatusTag = ({ status }: { status?: string }) => {
  const canonicalStatus = status?.trim()

  return (
    <Tag color={fieldStatusColor(canonicalStatus)} data-status={canonicalStatus ?? ''}>
      {fieldStatusLabel(canonicalStatus)}
    </Tag>
  )
}
