import { create } from 'zustand'
import type { BuildTrackData, ImportedBaseData } from '../../types/models'
import type { CustomReportRow, ReportFilters } from '../../types/reports'
import { DEFAULT_END_DATE, DEFAULT_MONTH, DEFAULT_START_DATE } from '../../utils/dateUtils'
import { clearDataset, loadDataset, saveDataset } from '../storage/db'
import { generateBuildTrackData } from './mockGenerator'
import { useAuthStore } from '../../features/auth/authStore'

export const defaultFilters: ReportFilters = {
  dateRange: [DEFAULT_START_DATE, DEFAULT_END_DATE],
  siteId: 'all',
  brigade: 'all',
  status: 'all',
  position: 'all',
  riskLevel: 'all',
  entryMethod: 'all',
  exportStatus: 'all',
  month: DEFAULT_MONTH,
  supervisor: 'all',
  reportType: 'all',
}

interface BuildTrackStore {
  data: BuildTrackData | null
  tenantWorkspaceId?: string
  filters: ReportFilters
  customReports: CustomReportRow[]
  loading: boolean
  initialized: boolean
  error: string
  setFilter: <K extends keyof ReportFilters>(key: K, value: ReportFilters[K]) => void
  resetFilters: () => void
  addCustomReport: (report: CustomReportRow) => void
  loadData: () => Promise<void>
  saveImportedData: (baseData: ImportedBaseData) => Promise<void>
  resetDemoData: () => Promise<void>
  generateSampleData: () => Promise<void>
}

const customReportStorageKey = 'buildtrack-custom-reports'

const loadCustomReports = (): CustomReportRow[] => {
  try {
    const raw = window.localStorage.getItem(customReportStorageKey)
    return raw ? (JSON.parse(raw) as CustomReportRow[]) : []
  } catch {
    return []
  }
}

const saveCustomReports = (reports: CustomReportRow[]) => {
  window.localStorage.setItem(customReportStorageKey, JSON.stringify(reports))
}

const emptyTenantData = (): BuildTrackData => ({
  company: [],
  sites: [],
  workers: [],
  assignments: [],
  workPhases: [],
  attendanceRecords: [],
  riskRecords: [],
  payrollRecords: [],
  supervisorAuditRecords: [],
  costCodeRecords: [],
  generatedAt: new Date().toISOString(),
  source: 'sample',
})

const isDemoTenant = () => useAuthStore.getState().tenant?.code?.toUpperCase() === 'DEMO'
const currentTenantWorkspaceId = () => useAuthStore.getState().tenant?.id ?? 'anonymous'

export const useBuildTrackStore = create<BuildTrackStore>((set, get) => ({
  data: null,
  tenantWorkspaceId: undefined,
  filters: defaultFilters,
  customReports: loadCustomReports(),
  loading: false,
  initialized: false,
  error: '',
  setFilter: (key, value) =>
    set((state) => ({
      filters: { ...state.filters, [key]: value },
    })),
  resetFilters: () => set({ filters: defaultFilters }),
  addCustomReport: (report) =>
    set((state) => {
      const customReports = [report, ...state.customReports]
      saveCustomReports(customReports)
      return { customReports }
    }),
  loadData: async () => {
    const tenantWorkspaceId = currentTenantWorkspaceId()
    if (get().loading || (get().initialized && get().tenantWorkspaceId === tenantWorkspaceId)) return

    set({ loading: true, error: '' })
    try {
      if (!isDemoTenant()) {
        set({ data: emptyTenantData(), tenantWorkspaceId, loading: false, initialized: true })
        return
      }

      const stored = await loadDataset()
      if (stored) {
        set({ data: stored, tenantWorkspaceId, loading: false, initialized: true })
        return
      }

      const sample = generateBuildTrackData()
      await saveDataset(sample)
      set({ data: sample, tenantWorkspaceId, loading: false, initialized: true })
    } catch (error) {
      const sample = generateBuildTrackData()
      set({
        data: sample,
        tenantWorkspaceId,
        loading: false,
        initialized: true,
        error: error instanceof Error ? error.message : 'Məlumat yüklənmədi',
      })
    }
  },
  saveImportedData: async (baseData) => {
    set({ loading: true, error: '' })
    const generated = generateBuildTrackData(baseData)
    await saveDataset(generated)
    set({ data: generated, tenantWorkspaceId: currentTenantWorkspaceId(), loading: false, initialized: true, filters: defaultFilters })
  },
  resetDemoData: async () => {
    set({ loading: true, error: '' })
    await clearDataset()
    const sample = generateBuildTrackData()
    await saveDataset(sample)
    set({ data: sample, tenantWorkspaceId: currentTenantWorkspaceId(), loading: false, initialized: true, filters: defaultFilters })
  },
  generateSampleData: async () => {
    set({ loading: true, error: '' })
    const sample = generateBuildTrackData()
    await saveDataset(sample)
    set({ data: sample, tenantWorkspaceId: currentTenantWorkspaceId(), loading: false, initialized: true, filters: defaultFilters })
  },
}))
