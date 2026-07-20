import type { CSSProperties } from 'react'
import { Select } from 'antd'
import { ALL_OBJECTS_ID, getObjects } from '../../features/projectProgress/projectSelectors'
import { useProjectProgressStore } from '../../features/projectProgress/projectProgressStore'

interface ObjectFilterProps {
  pageKey: string
  className?: string
  style?: CSSProperties
  placeholder?: string
}

export const ObjectFilter = ({ pageKey, className, placeholder = 'Obyekt seçin', style }: ObjectFilterProps) => {
  const store = useProjectProgressStore()
  const selectedObjectId = store.selectedObjectIdByPage[pageKey] ?? ALL_OBJECTS_ID
  const objects = getObjects(store)

  return (
    <Select
      className={className}
      value={selectedObjectId}
      placeholder={placeholder}
      onChange={(value) => store.setSelectedObjectForPage(pageKey, value)}
      style={{ minWidth: 220, ...style }}
      options={[
        { value: ALL_OBJECTS_ID, label: 'Bütün obyektlər' },
        ...objects.map((object) => ({ value: object.id, label: object.name })),
      ]}
    />
  )
}
