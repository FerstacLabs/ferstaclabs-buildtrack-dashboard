import type { AttendanceSessionRow } from '../../services/api/buildTrackBackendApi'

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
  row.workerExternalId?.trim()
  || row.workerName?.trim()
  || row.id

export const isLiveSessionCheckoutConfirmed = (row: AttendanceSessionRow) =>
  Boolean(row.isCheckoutConfirmed && (row.confirmedCheckOutTime || row.checkOutTime))

export const calculateLiveWorkedMinutes = (row: AttendanceSessionRow, nowMs: number) => {
  const startMs = Date.parse(row.checkInTime)
  if (!Number.isFinite(startMs)) return Math.max(0, row.workedMinutes ?? 0)

  const endSource = isLiveSessionCheckoutConfirmed(row)
    ? row.confirmedCheckOutTime ?? row.checkOutTime
    : undefined
  const endMs = endSource ? Date.parse(endSource) : nowMs
  if (!Number.isFinite(endMs) || endMs < startMs) return Math.max(0, row.workedMinutes ?? 0)

  return Math.max(0, Math.floor((endMs - startMs) / 60000))
}

export const deriveLiveAttendanceSummary = (rows: AttendanceSessionRow[]) => {
  const activeKeys = new Set<string>()
  const seenKeys = new Set<string>()
  let confirmedCheckouts = 0
  let totalWorkedMinutes = 0

  rows.forEach((row) => {
    const key = getLivePersonKey(row)
    seenKeys.add(key)
    totalWorkedMinutes += Math.max(0, row.workedMinutes ?? 0)
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
