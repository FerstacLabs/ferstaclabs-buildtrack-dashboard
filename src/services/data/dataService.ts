import { create } from 'zustand'
import type { BuildTrackData, ImportedBaseData } from '../../types/models'
import type { CustomReportRow, ReportFilters } from '../../types/reports'
import { DEFAULT_END_DATE, DEFAULT_MONTH, DEFAULT_START_DATE } from '../../utils/dateUtils'
import { clearDataset, loadDataset, saveDataset } from '../storage/db'
import { generateBuildTrackData } from './mockGenerator'

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

export const useBuildTrackStore = create<BuildTrackStore>((set, get) => ({
  data: null,
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
    if (get().loading || get().initialized) return

    set({ loading: true, error: '' })
    try {
      const stored = await loadDataset()
      if (stored) {
        set({ data: stored, loading: false, initialized: true })
        return
      }

      const sample = generateBuildTrackData()
      await saveDataset(sample)
      set({ data: sample, loading: false, initialized: true })
    } catch (error) {
      const sample = generateBuildTrackData()
      set({
        data: sample,
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
    set({ data: generated, loading: false, initialized: true, filters: defaultFilters })
  },
  resetDemoData: async () => {
    set({ loading: true, error: '' })
    await clearDataset()
    const sample = generateBuildTrackData()
    await saveDataset(sample)
    set({ data: sample, loading: false, initialized: true, filters: defaultFilters })
  },
  generateSampleData: async () => {
    set({ loading: true, error: '' })
    const sample = generateBuildTrackData()
    await saveDataset(sample)
    set({ data: sample, loading: false, initialized: true, filters: defaultFilters })
  },
}))
