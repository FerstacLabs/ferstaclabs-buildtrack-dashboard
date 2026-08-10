import { create } from 'zustand'
import {
  buildTrackBackendApi,
  type ProcurementTask,
  type SupplierRow,
  type SupplyDashboard,
  type SupplyNotification,
} from '../../services/api/buildTrackBackendApi'

interface SupplyPortalState {
  dashboard?: SupplyDashboard
  tasks: ProcurementTask[]
  suppliers: SupplierRow[]
  notifications: SupplyNotification[]
  loading: boolean
  error?: string
  load: () => Promise<void>
  loadTasks: () => Promise<void>
  loadSuppliers: () => Promise<void>
  loadNotifications: () => Promise<void>
}

const errorMessage = (error: unknown) => error instanceof Error ? error.message : 'Supply məlumatları yüklənmədi'

export const useSupplyPortalStore = create<SupplyPortalState>()((set) => ({
  tasks: [],
  suppliers: [],
  notifications: [],
  loading: false,
  load: async () => {
    set({ loading: true, error: undefined })
    try {
      const [dashboard, tasks, suppliers, notifications] = await Promise.all([
        buildTrackBackendApi.getSupplyDashboard(),
        buildTrackBackendApi.getSupplyTasks(),
        buildTrackBackendApi.getSuppliers(),
        buildTrackBackendApi.getSupplyNotifications(),
      ])
      set({ dashboard, tasks, suppliers, notifications, loading: false })
    } catch (error) {
      set({ loading: false, error: errorMessage(error) })
    }
  },
  loadTasks: async () => {
    set({ loading: true, error: undefined })
    try {
      set({ tasks: await buildTrackBackendApi.getSupplyTasks(), loading: false })
    } catch (error) {
      set({ loading: false, error: errorMessage(error) })
    }
  },
  loadSuppliers: async () => {
    try {
      set({ suppliers: await buildTrackBackendApi.getSuppliers() })
    } catch (error) {
      set({ error: errorMessage(error) })
    }
  },
  loadNotifications: async () => {
    try {
      set({ notifications: await buildTrackBackendApi.getSupplyNotifications() })
    } catch (error) {
      set({ error: errorMessage(error) })
    }
  },
}))

export const supplyStatusColor = (status?: string) => {
  if (status === 'Assigned' || status === 'Accepted') return 'blue'
  if (status === 'Shopping' || status === 'PartiallyCompleted') return 'orange'
  if (status === 'SubmittedForVerification') return 'purple'
  if (status === 'Verified' || status === 'Completed') return 'green'
  if (status === 'RejectedForCorrection' || status === 'Cancelled') return 'red'
  return 'default'
}

export const supplyStatusLabel = (status?: string) => ({
  Draft: 'Draft',
  Assigned: 'Təyin edilib',
  Accepted: 'Qəbul edilib',
  Shopping: 'Alışdadır',
  PartiallyCompleted: 'Qismən alınıb',
  Completed: 'Tamamlanıb',
  SubmittedForVerification: 'Təsdiqə göndərilib',
  Verified: 'Təsdiqlənib',
  RejectedForCorrection: 'Düzəliş lazımdır',
  Cancelled: 'Ləğv edilib',
}[status ?? ''] ?? status ?? '-')
