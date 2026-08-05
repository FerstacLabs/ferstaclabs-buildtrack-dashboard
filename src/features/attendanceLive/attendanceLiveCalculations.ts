import type { AttendanceSessionRow } from '../../services/api/buildTrackBackendApi'

export interface LiveAttendanceCards {
  activeWorkers: number
  todaySeen: number
  confirmedCheckouts: number
  totalWorkedMinutes: number
}

const stringField = (row: AttendanceSessionRow, names: string[]) => {
  const fields = row as unknown as Record<string, unknown>
  for (const name of names) {
    const value = fields[name]
    if (typeof value === 'string' && value.trim()) return value.trim()
  }
  return undefined
}

const booleanField = (row: AttendanceSessionRow, names: string[]) => {
  const fields = row as unknown as Record<string, unknown>
  for (const name of names) {
    const value = fields[name]
    if (typeof value === 'boolean') return value
  }
  return false
}

const parseDateMs = (value?: string) => {
  if (!value) return Number.NaN
  const parsed = Date.parse(value)
  return Number.isFinite(parsed) ? parsed : Number.NaN
}

export const formatLiveDuration = (minutes: number) => {
  const safeMinutes = Math.max(0, Math.floor(minutes))
  const hours = Math.floor(safeMinutes / 60)
  const rest = safeMinutes % 60
  if (hours === 0) return `${rest} dəq`
  return `${hours} saat ${rest} dəq`
}

export const formatLiveTotalDuration = (minutes: number) => {
  const safeMinutes = Math.max(0, Math.floor(minutes))
  if (safeMinutes < 60) return `${safeMinutes} dəq`
  return `${Math.round((safeMinutes / 60) * 10) / 10} saat`
}

export const getLivePersonKey = (row: AttendanceSessionRow) =>
  stringField(row, ['workerId', 'workerExternalId'])
  || `${stringField(row, ['workerName', 'cardName']) ?? 'unknown'}:${stringField(row, ['deviceId']) ?? ''}`
  || row.id

export const getLiveSessionStartMs = (row: AttendanceSessionRow) =>
  parseDateMs(stringField(row, ['firstSeen', 'firstSeenAt', 'checkInTime', 'startedAt', 'checkIn']))

export const getLiveSessionCheckoutMs = (row: AttendanceSessionRow) =>
  parseDateMs(stringField(row, ['confirmedCheckOutTime', 'checkOutTime', 'checkoutAt']))

export const isLiveSessionCheckoutConfirmed = (row: AttendanceSessionRow) => {
  const checkoutMs = getLiveSessionCheckoutMs(row)
  return Number.isFinite(checkoutMs) || booleanField(row, ['confirmedCheckout', 'isCheckoutConfirmed'])
}

export const calculateLiveWorkedMinutes = (row: AttendanceSessionRow, nowMs: number) => {
  const startMs = getLiveSessionStartMs(row)
  if (!Number.isFinite(startMs)) return Math.max(0, row.workedMinutes ?? 0)

  const checkoutMs = getLiveSessionCheckoutMs(row)
  const endMs = Number.isFinite(checkoutMs) ? checkoutMs : nowMs
  if (!Number.isFinite(endMs) || endMs < startMs) return Math.max(0, row.workedMinutes ?? 0)

  return Math.max(0, Math.floor((endMs - startMs) / 60000))
}

export const deriveLiveAttendanceSummary = (rows: AttendanceSessionRow[], nowMs: number) => {
  const activeKeys = new Set<string>()
  const seenKeys = new Set<string>()
  let confirmedCheckouts = 0
  let totalWorkedMinutes = 0

  rows.forEach((row) => {
    const key = getLivePersonKey(row)
    seenKeys.add(key)
    totalWorkedMinutes += calculateLiveWorkedMinutes(row, nowMs)
    if (isLiveSessionCheckoutConfirmed(row)) {
      confirmedCheckouts += 1
    } else {
      activeKeys.add(key)
    }
  })

  return {
    activeWorkers: activeKeys.size,
    todaySeen: seenKeys.size,
    confirmedCheckouts,
    totalWorkedMinutes,
  }
}

export const deriveLiveAttendanceCards = (
  rows: AttendanceSessionRow[],
  nowMs: number,
  fallbacks: Partial<LiveAttendanceCards> = {},
): LiveAttendanceCards => {
  if (rows.length > 0) return deriveLiveAttendanceSummary(rows, nowMs)

  return {
    activeWorkers: Math.max(0, fallbacks.activeWorkers ?? 0),
    todaySeen: Math.max(0, fallbacks.todaySeen ?? fallbacks.activeWorkers ?? 0),
    confirmedCheckouts: Math.max(0, fallbacks.confirmedCheckouts ?? 0),
    totalWorkedMinutes: Math.max(0, fallbacks.totalWorkedMinutes ?? 0),
  }
}
