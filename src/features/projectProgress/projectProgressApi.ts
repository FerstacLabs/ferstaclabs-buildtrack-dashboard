import type { Crew, ProjectEstimateSummary, ProjectProgressData, WorkItem, WorkStage } from '../../types/projectProgress'
import { apiRequest, tryApiRequest } from '../../shared/api/client'

export const projectProgressApi = {
  getWorkspace: () => tryApiRequest<ProjectProgressData>('/api/project-progress/workspace'),
  saveWorkspace: (workspace: ProjectProgressData) => tryApiRequest<{ saved: boolean; updatedAt: string }>('/api/project-progress/workspace', {
    method: 'PUT',
    body: JSON.stringify(workspace),
  }),
  importLegacyWorkspace: (workspace: ProjectProgressData) => apiRequest<{ imported: boolean; legacyBrowserImportedAt?: string }>('/api/project-progress/import-legacy', {
    method: 'POST',
    body: JSON.stringify(workspace),
  }),
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
