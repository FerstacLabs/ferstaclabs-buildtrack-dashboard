import type { CSSProperties } from 'react'
import { ProjectSelect } from '../ProjectSelect'

interface ObjectFilterProps {
  pageKey: string
  className?: string
  style?: CSSProperties
  placeholder?: string
}

export const ObjectFilter = (props: ObjectFilterProps) => <ProjectSelect {...props} />
