import { create } from 'zustand'
import { buildTrackBackendApi, type FieldAssignment, type FieldMe } from '../../services/api/buildTrackBackendApi'

interface FieldPortalState {
  me?: FieldMe
  assignments: FieldAssignment[]
  selectedSiteId?: string
  loading: boolean
  error?: string
  load: () => Promise<void>
  setSelectedSiteId: (siteId: string) => void
}

export const useFieldPortalStore = create<FieldPortalState>()((set, get) => ({
  assignments: [],
  loading: false,
  load: async () => {
    set({ loading: true, error: undefined })
    try {
      const me = await buildTrackBackendApi.getFieldMe()
      const selectedSiteId = me.assignments.some((assignment) => assignment.siteId === get().selectedSiteId)
        ? get().selectedSiteId
        : me.assignments[0]?.siteId
      set({ me, assignments: me.assignments, selectedSiteId, loading: false })
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Field portal məlumatları yüklənmədi'
      set({ loading: false, error: message })
    }
  },
  setSelectedSiteId: (siteId) => set({ selectedSiteId: siteId }),
}))

export const fieldStatusColor = (status?: string) => {
  switch (status) {
    case 'Approved':
    case 'Issued':
    case 'Closed':
    case 'Reviewed':
      return 'green'
    case 'Submitted':
    case 'PendingApproval':
    case 'ReadyForPickup':
      return 'blue'
    case 'NeedsCorrection':
    case 'NeedsJustification':
    case 'PartiallyApproved':
      return 'orange'
    case 'Rejected':
    case 'Cancelled':
      return 'red'
    default:
      return 'default'
  }
}

export const fieldStatusLabel = (status?: string) => {
  switch (status) {
    case 'Draft': return 'Qaralama'
    case 'Submitted': return 'Göndərilib'
    case 'Approved': return 'Təsdiqlənib'
    case 'NeedsCorrection': return 'Düzəliş lazımdır'
    case 'Rejected': return 'Rədd edilib'
    case 'NeedsJustification': return 'Əsaslandırma lazımdır'
    case 'PendingApproval': return 'Təsdiq gözləyir'
    case 'PartiallyApproved': return 'Qismən təsdiq'
    case 'ReadyForPickup': return 'Təhvilə hazır'
    case 'Issued': return 'Verilib'
    case 'Closed': return 'Bağlanıb'
    case 'Cancelled': return 'Ləğv edilib'
    case 'Open': return 'Açıq'
    default: return status ?? 'Naməlum'
  }
}
