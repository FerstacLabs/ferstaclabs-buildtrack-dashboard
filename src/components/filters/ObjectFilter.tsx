import { useEffect, useMemo } from 'react'
import type { CSSProperties } from 'react'
import { Select } from 'antd'
import { ALL_OBJECTS_ID } from '../../features/projectProgress/projectSelectors'
import { useProjectProgressStore } from '../../features/projectProgress/projectProgressStore'
import { useI18n } from '../../i18n'

interface ObjectFilterProps {
  pageKey: string
  className?: string
  style?: CSSProperties
  placeholder?: string
}

export const ObjectFilter = ({ pageKey, className, placeholder, style }: ObjectFilterProps) => {
  const selectedObjectId = useProjectProgressStore((state) => state.selectedObjectId)
  const objects = useProjectProgressStore((state) => state.objects)
  const setSelectedObjectForPage = useProjectProgressStore((state) => state.setSelectedObjectForPage)
  const { t } = useI18n()

  const selectedValue = selectedObjectId && (
    selectedObjectId === ALL_OBJECTS_ID || objects.some((object) => object.id === selectedObjectId)
  )
    ? selectedObjectId
    : ALL_OBJECTS_ID

  const options = useMemo(() => [
    { value: ALL_OBJECTS_ID, label: t('project.allObjects') },
    ...objects.map((object) => ({ value: object.id, label: object.name })),
  ], [objects, t])

  useEffect(() => {
    if (selectedValue !== selectedObjectId) {
      setSelectedObjectForPage(pageKey, selectedValue)
    }
  }, [pageKey, selectedObjectId, selectedValue, setSelectedObjectForPage])

  const handleChange = (value: string) => {
    if (import.meta.env.DEV) {
      console.debug('[BuildTrack] project/object filter changed', {
        pageKey,
        previous: selectedValue,
        next: value,
      })
    }
    setSelectedObjectForPage(pageKey, value)
  }

  return (
    <Select
      className={className}
      value={selectedValue}
      placeholder={placeholder ?? t('project.selectProject')}
      onChange={handleChange}
      style={{ minWidth: 220, ...style }}
      options={options}
    />
  )
}
