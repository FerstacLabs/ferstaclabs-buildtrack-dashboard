export type DeviceMode = 'ActiveRegister' | 'CgiPollingFallback' | 'Simulator'
export type DeviceStatus = 'Pending' | 'Online' | 'Offline' | 'Error'
export type AttendanceStatus = 'Ok' | 'Failed' | 'Stranger'
export type AttendanceMethod = 'Face' | 'Card' | 'Fingerprint' | 'Password' | 'Manual' | 'Unknown'

export interface BackendSite {
  id: string
  name: string
  address: string
  timeZone: string
  createdAt: string
}

export interface BackendDevice {
  id: string
  siteId: string
  name: string
  vendor: string
  model: string
  mode: DeviceMode
  registerDeviceId: string
  registerPort: number
  lastKnownIp?: string
  lastSeenAt?: string
  status: DeviceStatus
  lastRecNo?: number
  createdAt: string
  updatedAt: string
  lastEventAt?: string
  lastEventWorkerName?: string
  netSdkDecodeStatus: string
}

export interface DeviceConnectionLog {
  id: string
  deviceId?: string
  registerDeviceId?: string
  remoteIp: string
  remotePort: number
  eventType: string
  message: string
  rawPayloadJson?: unknown
  createdAt: string
}

export interface AttendanceLiveEvent {
  id: string
  siteId: string
  siteName?: string
  deviceId: string
  deviceName?: string
  workerId?: string
  workerExternalId?: string
  workerName?: string
  eventTime: string
  direction: 'Entry' | 'Exit' | 'Unknown'
  status: AttendanceStatus
  method: AttendanceMethod
  rawRecNo?: number
  snapshotPath?: string
  source: string
  createdAt: string
}

export interface AttendanceLiveWorker {
  workerExternalId: string
  workerName?: string
  checkInTime: string
  checkInTimeLocal: string
  lastSeenTime?: string
  lastSeenTimeLocal?: string
  confirmedCheckOutTime?: string
  confirmedCheckOutTimeLocal?: string
  closeReason?: string
  displayStatus?: string
  isCheckoutConfirmed?: boolean
  workedMinutesSoFar: number
  status: 'Open' | 'Closed'
}

export interface AttendanceLiveStatus {
  workDate?: string
  activeWorkersCount: number
  workers: AttendanceLiveWorker[]
  staleOpenSessionsCount?: number
}

export interface AttendanceSessionRow {
  id: string
  workerExternalId: string
  workerName?: string
  checkInTime: string
  checkOutTime?: string
  checkInTimeLocal: string
  checkOutTimeLocal?: string
  lastSeenTime?: string
  lastSeenTimeLocal?: string
  confirmedCheckOutTime?: string
  confirmedCheckOutTimeLocal?: string
  closeReason?: string
  displayStatus?: string
  isCheckoutConfirmed?: boolean
  workedMinutes: number
  status: 'Open' | 'Closed'
  source: string
}

export interface AttendanceDailySummary {
  workDate: string
  totalWorkersCheckedIn: number
  activeWorkersCount: number
  closedSessionsCount: number
  totalWorkedHours: number
  sessions: AttendanceSessionRow[]
}

export type SecurityEventStatus = 'Open' | 'Reviewed' | 'Ignored'

export interface SecurityEventRow {
  id: string
  eventTime: string
  eventTimeLocal: string
  eventType: 'UnknownFace'
  severity: 'Warning'
  status: SecurityEventStatus
  deviceName?: string
  siteName?: string
  snapshotPath?: string
  snapshotUrl: string
  snapshotDownloadStatus?: string
  snapshotDownloadError?: string
  snapshotSource?: string
  message?: string
  rawRecNo?: number
}

export interface ListenerStatus {
  ports: number[]
  defaultPorts: number[]
  realSdkAvailable: boolean
  simulatorEnabled: boolean
  decodeStatus?: string
  warning?: string
}

export class BackendApiError extends Error {
  readonly url: string
  readonly status: number
  readonly details: string

  constructor(message: string, url: string, status: number, details: string) {
    super(message)
    this.name = 'BackendApiError'
    this.url = url
    this.status = status
    this.details = details
  }
}

const API_BASE = ((import.meta.env.VITE_API_BASE_URL as string | undefined) ?? (import.meta.env.VITE_BUILDTRACK_API_URL as string | undefined))?.replace(/\/$/, '') ?? 'http://localhost:8080'

const parseJsonBody = (text: string) => {
  if (!text) return undefined
  try {
    return JSON.parse(text) as unknown
  } catch {
    return text
  }
}

const unwrapValue = <T>(payload: unknown): T => {
  if (payload && typeof payload === 'object' && 'value' in payload) return (payload as { value: T }).value
  return payload as T
}

const unwrapArray = <T>(payload: unknown): T[] => {
  const value = unwrapValue<unknown>(payload)
  if (Array.isArray(value)) return value as T[]
  console.warn('BuildTrack backend response was not an array', { payload })
  return []
}

const normalizeAttendanceLiveStatus = (payload: unknown): AttendanceLiveStatus => {
  const value = unwrapValue<Partial<AttendanceLiveStatus>>(payload) ?? {}
  const workers = Array.isArray(value.workers) ? value.workers : []
  return {
    workDate: typeof value.workDate === 'string' ? value.workDate : undefined,
    activeWorkersCount: Number(value.activeWorkersCount ?? workers.length ?? 0),
    workers,
    staleOpenSessionsCount: Number(value.staleOpenSessionsCount ?? 0),
  }
}

const normalizeAttendanceDaily = (payload: unknown): AttendanceDailySummary => {
  const value = unwrapValue<Partial<AttendanceDailySummary>>(payload) ?? {}
  const sessions = Array.isArray(value.sessions) ? value.sessions : []
  return {
    workDate: typeof value.workDate === 'string' ? value.workDate : '',
    totalWorkersCheckedIn: Number(value.totalWorkersCheckedIn ?? sessions.length ?? 0),
    activeWorkersCount: Number(value.activeWorkersCount ?? sessions.filter((session) => session.status === 'Open').length ?? 0),
    closedSessionsCount: Number(value.closedSessionsCount ?? sessions.filter((session) => session.status === 'Closed').length ?? 0),
    totalWorkedHours: Number(value.totalWorkedHours ?? 0),
    sessions,
  }
}

const request = async <T>(path: string, init?: RequestInit): Promise<T> => {
  const url = `${API_BASE}${path}`
  const response = await fetch(url, {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      ...init?.headers,
    },
  })
  const text = response.status === 204 ? '' : await response.text()
  const parsed = parseJsonBody(text)
  console.debug('BuildTrack backend response', { url, status: response.status, parsed })

  if (!response.ok) {
    console.error('BuildTrack backend request failed', { url, status: response.status, parsed })
    throw new BackendApiError(typeof parsed === 'string' ? parsed : JSON.stringify(parsed ?? ''), url, response.status, text)
  }

  return parsed as T
}

export const buildTrackBackendApi = {
  baseUrl: API_BASE,
  getSites: async () => unwrapArray<BackendSite>(await request<unknown>('/api/sites')),
  createSite: (body: { name: string; address: string; timeZone: string }) => request<BackendSite>('/api/sites', { method: 'POST', body: JSON.stringify(body) }),
  createWorker: (body: { siteId: string; externalWorkerCode: string; fullName: string; status?: string }) => request('/api/workers', { method: 'POST', body: JSON.stringify(body) }),
  getDevices: async () => unwrapArray<BackendDevice>(await request<unknown>('/api/devices')),
  getDeviceLogs: async (id: string) => unwrapArray<DeviceConnectionLog>(await request<unknown>(`/api/devices/${id}/logs`)),
  createDevice: (body: Record<string, unknown>) => request<BackendDevice>('/api/devices', { method: 'POST', body: JSON.stringify(body) }),
  markReady: (id: string) => request<BackendDevice>(`/api/devices/${id}/mark-active-register-ready`, { method: 'POST' }),
  simulateRegister: (id: string) => request<BackendDevice>(`/api/devices/${id}/simulate-active-register`, { method: 'POST' }),
  simulateEvent: (id: string) => request<AttendanceLiveEvent>(`/api/devices/${id}/simulate-event`, { method: 'POST', body: JSON.stringify({}) }),
  getAttendanceLive: async (siteId: string) => unwrapArray<AttendanceLiveEvent>(await request<unknown>(`/api/sites/${siteId}/attendance-live?limit=100`)),
  getAttendanceLiveStatus: async (siteId: string) => normalizeAttendanceLiveStatus(await request<unknown>(`/api/sites/${siteId}/attendance/live-status`)),
  getAttendanceDaily: async (siteId: string, date?: string) => normalizeAttendanceDaily(await request<unknown>(`/api/sites/${siteId}/attendance/daily${date ? `?date=${date}` : ''}`)),
  getSecurityEvents: async (siteId: string, date?: string) => unwrapArray<SecurityEventRow>(await request<unknown>(`/api/sites/${siteId}/security-events${date ? `?date=${date}` : ''}`)),
  reviewSecurityEvent: (id: string, body: { status: SecurityEventStatus; reviewNote?: string }) => request(`/api/security-events/${id}/review`, { method: 'PATCH', body: JSON.stringify(body) }),
  securitySnapshotUrl: (snapshotUrl: string) => `${API_BASE}${snapshotUrl}`,
  getListenerStatus: () => request<ListenerStatus>('/api/dahua/listener/status'),
}
