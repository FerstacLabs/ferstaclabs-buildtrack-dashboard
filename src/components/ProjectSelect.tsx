import { useEffect, useMemo } from 'react'
import type { CSSProperties } from 'react'
import { Select } from 'antd'
import { useI18n } from '../i18n'
import { useProjectProgressStore } from '../features/projectProgress/projectProgressStore'
import {
  ALL_PROJECTS_ID,
  normalizeSelectedProjectId,
  useProjectSelectionStore,
} from '../stores/projectSelectionStore'

interface ProjectSelectProps {
  pageKey?: string
  className?: string
  style?: CSSProperties
  placeholder?: string
}

export const ProjectSelect = ({ pageKey = 'global', className, placeholder, style }: ProjectSelectProps) => {
  const { t } = useI18n()
  const objects = useProjectProgressStore((state) => state.objects)
  const selectedProjectId = useProjectSelectionStore((state) => state.selectedProjectId)
  const setSelectedProjectId = useProjectSelectionStore((state) => state.setSelectedProjectId)
  const ensureSelectedProjectId = useProjectSelectionStore((state) => state.ensureSelectedProjectId)

  const projectIds = useMemo(() => objects.map((object) => object.id), [objects])
  const selectedValue = normalizeSelectedProjectId(selectedProjectId, projectIds)

  if (import.meta.env.DEV) {
    console.debug('[ProjectSelect render]', {
      pageKey,
      selectedProjectId,
      selectedValue,
    })
  }

  const options = useMemo(() => [
    { value: ALL_PROJECTS_ID, label: t('project.allObjects') },
    ...objects.map((object) => ({ value: object.id, label: object.name })),
  ], [objects, t])

  useEffect(() => {
    ensureSelectedProjectId(projectIds)
  }, [ensureSelectedProjectId, projectIds])

  const handleChange = (nextProjectId: string) => {
    if (import.meta.env.DEV) {
      console.debug('[ProjectSelect onChange]', {
        pageKey,
        previous: selectedValue,
        next: nextProjectId,
      })
    }

    setSelectedProjectId(nextProjectId)
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
