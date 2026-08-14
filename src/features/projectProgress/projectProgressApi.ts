import type { Crew, MaterialItem, Project, ProjectEstimateSummary, ProjectProgressData, WorkItem, WorkStage } from '../../types/projectProgress'
import { apiRequest, tryApiRequest } from '../../shared/api/client'

export const projectProgressApi = {
  getWorkspace: () => tryApiRequest<ProjectProgressData>('/api/project-progress/workspace'),
  getWorkspaceStrict: () => apiRequest<ProjectProgressData>('/api/project-progress/workspace'),
  // Compatibility-only path for the visible legacy migration/manual-save workflow.
  saveWorkspace: (workspace: ProjectProgressData) => apiRequest<{ saved: boolean; updatedAt: string }>('/api/project-progress/workspace', {
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
  getProjects: () => apiRequest<Project[]>('/api/projects'),
  createProject: (project: Partial<Project>) => apiRequest<Project>('/api/projects', {
    method: 'POST',
    body: JSON.stringify(project),
  }),
  createStage: (projectId: string, stage: Omit<WorkStage, 'id' | 'order'> & Partial<Pick<WorkStage, 'id' | 'order'>>) => apiRequest<WorkStage>(`/api/projects/${projectId}/stages`, {
    method: 'POST',
    body: JSON.stringify(stage),
  }),
  saveStage: (stage: WorkStage) => apiRequest<WorkStage>(`/api/project-stages/${stage.id}`, {
    method: 'PUT',
    body: JSON.stringify(stage),
  }),
  deleteStage: (stageId: string) => apiRequest<void>(`/api/project-stages/${stageId}`, {
    method: 'DELETE',
  }),
  createWorkItem: (projectId: string, workItem: Omit<WorkItem, 'id'> & Partial<Pick<WorkItem, 'id'>>) => apiRequest<WorkItem>(`/api/projects/${projectId}/work-items`, {
    method: 'POST',
    body: JSON.stringify(workItem),
  }),
  saveWorkItem: (workItem: WorkItem) => apiRequest<WorkItem>(`/api/project-work-items/${workItem.id}`, {
    method: 'PUT',
    body: JSON.stringify(workItem),
  }),
  updateWorkItem: (workItemId: string, patch: Partial<WorkItem>) => apiRequest<WorkItem>(`/api/project-work-items/${workItemId}`, {
    method: 'PUT',
    body: JSON.stringify({ ...patch, id: workItemId }),
  }),
  deleteWorkItem: (workItemId: string) => apiRequest<void>(`/api/project-work-items/${workItemId}`, {
    method: 'DELETE',
  }),
  createCrew: (projectId: string, crew: Omit<Crew, 'id'> & Partial<Pick<Crew, 'id'>>) => apiRequest<Crew>(`/api/projects/${projectId}/crews`, {
    method: 'POST',
    body: JSON.stringify(crew),
  }),
  saveCrew: (crew: Crew) => apiRequest<Crew>(`/api/project-crews/${crew.id}`, {
    method: 'PUT',
    body: JSON.stringify(crew),
  }),
  deleteCrew: (crewId: string) => apiRequest<void>(`/api/project-crews/${crewId}`, {
    method: 'DELETE',
  }),
  createMaterial: (projectId: string, material: Omit<MaterialItem, 'id' | 'remainingQuantity'> & Partial<Pick<MaterialItem, 'id' | 'remainingQuantity'>>) => apiRequest<MaterialItem>(`/api/projects/${projectId}/materials`, {
    method: 'POST',
    body: JSON.stringify(material),
  }),
  saveMaterial: (material: MaterialItem) => apiRequest<MaterialItem>(`/api/project-materials/${material.id}`, {
    method: 'PUT',
    body: JSON.stringify(material),
  }),
  deleteMaterial: (materialId: string) => apiRequest<void>(`/api/project-materials/${materialId}`, {
    method: 'DELETE',
  }),
}
