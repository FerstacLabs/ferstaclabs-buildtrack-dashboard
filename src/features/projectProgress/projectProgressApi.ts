import type { Crew, ProjectEstimateSummary, WorkItem, WorkStage } from '../../types/projectProgress'
import { tryApiRequest } from '../../shared/api/client'

export const projectProgressApi = {
  getSummary: () => tryApiRequest<ProjectEstimateSummary>('/api/project-progress/summary'),
  getStages: () => tryApiRequest<WorkStage[]>('/api/project-progress/stages'),
  getWorkItems: () => tryApiRequest<WorkItem[]>('/api/project-progress/work-items'),
  getCrews: () => tryApiRequest<Crew[]>('/api/project-progress/crews'),
  saveStage: (stage: WorkStage) => tryApiRequest<WorkStage>(`/api/project-progress/stages/${stage.id}`, {
    method: 'PUT',
    body: JSON.stringify(stage),
  }),
  saveWorkItem: (workItem: WorkItem) => tryApiRequest<WorkItem>(`/api/project-progress/work-items/${workItem.id}`, {
    method: 'PUT',
    body: JSON.stringify(workItem),
  }),
  saveCrew: (crew: Crew) => tryApiRequest<Crew>(`/api/project-progress/crews/${crew.id}`, {
    method: 'PUT',
    body: JSON.stringify(crew),
  }),
}
