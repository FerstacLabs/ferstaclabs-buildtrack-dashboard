import type { CSSProperties } from 'react'
import { Select } from 'antd'
import { ALL_OBJECTS_ID, getObjects } from '../../features/projectProgress/projectSelectors'
import { useProjectProgressStore } from '../../features/projectProgress/projectProgressStore'
import { useI18n } from '../../i18n'

interface ObjectFilterProps {
  pageKey: string
  className?: string
  style?: CSSProperties
  placeholder?: string
}

export const ObjectFilter = ({ pageKey, className, placeholder = 'Obyekt seçin', style }: ObjectFilterProps) => {
  const store = useProjectProgressStore()
  const { t } = useI18n()
  const selectedObjectId = store.selectedObjectId
  const objects = getObjects(store)

  return (
    <Select
      className={className}
      value={selectedObjectId}
      placeholder={placeholder === 'Obyekt seçin' ? t('project.selectProject') : placeholder}
      onChange={(value) => store.setSelectedObjectForPage(pageKey, value)}
      style={{ minWidth: 220, ...style }}
      options={[
        { value: ALL_OBJECTS_ID, label: t('project.allObjects') },
        ...objects.map((object) => ({ value: object.id, label: object.name })),
      ]}
    />
  )
}
