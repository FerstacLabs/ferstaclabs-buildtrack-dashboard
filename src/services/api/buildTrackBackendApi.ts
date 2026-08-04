import { API_BASE_URL } from '../../shared/api/client'
import { authHeader } from '../../features/auth/authToken'

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

export interface WorkerCameraIdentity {
  id: string
  workerId: string
  deviceId?: string
  deviceName?: string
  vendor: string
  externalUserId?: string
  cardName?: string
  normalizedCardName?: string
  isPrimary: boolean
  createdAt: string
  updatedAt?: string
}

export interface WorkerSiteAssignment {
  id: string
  workerId: string
  siteId: string
  siteName?: string
  isPrimary: boolean
  status: 'Active' | 'Inactive'
  createdAt: string
  updatedAt?: string
}

export interface BackendWorker {
  id: string
  siteId: string
  externalWorkerCode: string
  fullName: string
  status: 'Active' | 'Inactive'
  brigade?: string
  role?: string
  hourlyRate: number
  plannedDailyHours: number
  attendanceSource: 'Camera' | 'Manual' | 'ForemanTablet'
  riskScore: number
  notes?: string
  createdAt: string
  updatedAt?: string
  cameraIdentities: WorkerCameraIdentity[]
  siteAssignments: WorkerSiteAssignment[]
  payrollSummary: {
    todayCameraHours: number
    todayEstimatedPay: number
    todayEstimatedAmount?: number
    monthlyCameraHours: number
    monthlyEstimatedPay: number
    monthlyEstimatedAmount?: number
    isCurrentlyActive: boolean
    currentSessionStartedAt?: string
    lastSeenAt?: string
  }
}

export interface SaveWorkerBody {
  siteId: string
  externalWorkerCode: string
  fullName: string
  status?: 'Active' | 'Inactive'
  brigade?: string
  role?: string
  hourlyRate?: number
  plannedDailyHours?: number
  attendanceSource?: 'Camera' | 'Manual' | 'ForemanTablet'
  riskScore?: number
  notes?: string
  siteAssignments?: {
    siteId: string
    isPrimary?: boolean
  }[]
  cameraIdentity?: {
    deviceId?: string
    externalUserId?: string
    cardName?: string
    isPrimary?: boolean
  }
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
  snapshotUrl?: string
  source: string
  createdAt: string
}

export type LicensePlan = 'Trial' | 'Starter' | 'Business' | 'Enterprise' | 'Unlimited'
export type LicenseStatus = 'Pending' | 'Active' | 'Expired' | 'Revoked'

export interface LicenseResponse {
  id: string
  tenantId: string
  plan: LicensePlan
  status: LicenseStatus
  startsAt: string
  expiresAt?: string
  maxProjects?: number
  maxUsers?: number
  maxCameras?: number
}

export interface AdminTenantLicenseRow {
  tenantId: string
  companyName: string
  ownerEmail?: string
  tenantStatus: 'Active' | 'Suspended'
  licensePlan?: LicensePlan
  licenseStatus?: LicenseStatus
  expiresAt?: string
  maxProjects?: number
  maxUsers?: number
  maxCameras?: number
  createdAt: string
  licenseId?: string
}

export interface CreateAdminLicenseResponse {
  licenseKey: string
  license: LicenseResponse
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
  method?: AttendanceMethod
  snapshotPath?: string
  snapshotUrl?: string
}

export interface AttendanceDailySummary {
  workDate: string
  totalWorkersCheckedIn: number
  activeWorkersCount: number
  closedSessionsCount: number
  totalWorkedHours: number
  sessions: AttendanceSessionRow[]
}

export interface AttendanceSnapshotRow {
  id: string
  eventTime: string
  eventTimeLocal: string
  snapshotUrl?: string
  method: AttendanceMethod
  source: string
}

export type SecurityEventStatus = 'Open' | 'Reviewed' | 'Ignored' | 'AutoResolved'

export interface SecurityEventRow {
  id: string
  eventTime: string
  eventTimeLocal: string
  eventType: 'UnknownFace' | 'SuspiciousRecognition' | 'IdentityMismatch' | 'IdentityMappingConflict' | 'ParserUncertainSmartEvent' | 'UnmappedCameraIdentity'
  severity: 'Warning'
  status: SecurityEventStatus
  deviceName?: string
  siteName?: string
  snapshotPath?: string
  snapshotUrl?: string
  snapshotDownloadStatus?: string
  snapshotDownloadError?: string
  snapshotSource?: string
  message?: string
  rawRecNo?: number
  cameraExternalUserId?: string
  cameraCardName?: string
}


export interface ActiveRegisterStatus {
  enabled: boolean
  listenerActive: boolean
  ports: number[]
  lastCallbackTime?: string
  lastCommand?: string
  lastPayloadBytes: number
  rawEventCount: number
  decodedEventCount: number
  ingestedEventCount: number
  ingestionEnabled: boolean
  diagnosticsEnabled: boolean
  decodeStatus?: string
  warning?: string
}

export interface ActiveRegisterRawEventRow {
  id: string
  deviceId?: string
  registerDeviceId?: string
  remoteIp?: string
  remotePort?: number
  listenerPort: number
  callbackCommand: number
  callbackCommandName?: string
  payloadBytes: number
  payloadFirstBytesHex?: string
  decodeStatus: string
  decodedJson?: string
  createdAt: string
}
export interface ListenerStatus {
  ports: number[]
  defaultPorts: number[]
  realSdkAvailable: boolean
  simulatorEnabled: boolean
  decodeStatus?: string
  warning?: string
  apiConfig?: {
    enabled: boolean
    ingestionEnabled: boolean
    ports: number[]
  }
  workerDiagnosticsPresent?: boolean
  workerListenerActive?: boolean
  lastDecodeStatus?: string
  lastLoginStrategy?: string
  lastLoginSucceeded?: boolean
  lastLoginErrorSigned?: number
  lastLoginErrorHex?: string
  lastLoginNativeErrorSigned?: number
  lastLoginNativeErrorHex?: string
  loginPossibleMarshallingWarning?: boolean
  startListenExSucceeded?: boolean
  startListenExErrorHex?: string
  worker?: Record<string, unknown>}

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

const API_BASE = API_BASE_URL

const withApiBase = (path?: string) => {
  if (!path) return ''
  if (/^https?:\/\//i.test(path)) return path

  const normalizedPath = path.startsWith('/') ? path : `/${path}`
  if (!API_BASE) return normalizedPath
  if (normalizedPath === API_BASE || normalizedPath.startsWith(`${API_BASE}/`)) return normalizedPath

  return `${API_BASE}${normalizedPath}`
}

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
  const headers = new Headers(init?.headers)
  headers.set('Content-Type', headers.get('Content-Type') ?? 'application/json')
  Object.entries(authHeader()).forEach(([key, value]) => headers.set(key, value))
  const response = await fetch(url, {
    ...init,
    headers,
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
  getWorkers: async (siteId?: string) => unwrapArray<BackendWorker>(await request<unknown>(`/api/workers${siteId ? `?siteId=${encodeURIComponent(siteId)}` : ''}`)),
  createWorker: (body: SaveWorkerBody) => request<BackendWorker>('/api/workers', { method: 'POST', body: JSON.stringify(body) }),
  updateWorker: (id: string, body: SaveWorkerBody) => request<BackendWorker>(`/api/workers/${id}`, { method: 'PUT', body: JSON.stringify(body) }),
  deleteWorker: (id: string) => request<void>(`/api/workers/${id}`, { method: 'DELETE' }),
  testWorkerCameraIdentity: (id: string, body: { deviceId?: string; externalUserId?: string; cardName?: string }) =>
    request<{ matched: boolean; workerId?: string; workerName?: string; workerCode?: string; resolvedBy?: string; status?: string; reason?: string }>(`/api/workers/${id}/camera-identities/test`, { method: 'POST', body: JSON.stringify(body) }),
  remapWorkerCameraIdentity: (id: string, identityId?: string) =>
    request<{ attendanceEventsUpdated: number; attendanceSessionsUpdated: number }>(`/api/workers/${id}/camera-identities/remap${identityId ? `?identityId=${encodeURIComponent(identityId)}` : ''}`, { method: 'POST' }),
  remapWorkerCameraEvents: (id: string, identityId?: string) =>
    request<{ attendanceEventsUpdated: number; attendanceSessionsUpdated: number }>(`/api/workers/${id}/remap-camera-events${identityId ? `?identityId=${encodeURIComponent(identityId)}` : ''}`, { method: 'POST' }),
  getDevices: async () => unwrapArray<BackendDevice>(await request<unknown>('/api/devices')),
  getDeviceLogs: async (id: string) => unwrapArray<DeviceConnectionLog>(await request<unknown>(`/api/devices/${id}/logs`)),
  createDevice: (body: Record<string, unknown>) => request<BackendDevice>('/api/devices', { method: 'POST', body: JSON.stringify(body) }),
  markReady: (id: string) => request<BackendDevice>(`/api/devices/${id}/mark-active-register-ready`, { method: 'POST' }),
  simulateRegister: (id: string) => request<BackendDevice>(`/api/devices/${id}/simulate-active-register`, { method: 'POST' }),
  simulateEvent: (id: string) => request<AttendanceLiveEvent>(`/api/devices/${id}/simulate-event`, { method: 'POST', body: JSON.stringify({}) }),
  getAttendanceLive: async (siteId: string) => unwrapArray<AttendanceLiveEvent>(await request<unknown>(`/api/sites/${siteId}/attendance-live?limit=100`)),
  getAttendanceLiveStatus: async (siteId: string) => normalizeAttendanceLiveStatus(await request<unknown>(`/api/sites/${siteId}/attendance/live-status`)),
  getAttendanceDaily: async (siteId: string, date?: string) => normalizeAttendanceDaily(await request<unknown>(`/api/sites/${siteId}/attendance/daily${date ? `?date=${date}` : ''}`)),
  getAttendanceSnapshots: async (siteId: string, workerExternalId: string, date: string) => unwrapArray<AttendanceSnapshotRow>(await request<unknown>(`/api/attendance-events/snapshots?siteId=${encodeURIComponent(siteId)}&workerExternalId=${encodeURIComponent(workerExternalId)}&date=${encodeURIComponent(date)}`)),
  getSecurityEvents: async (siteId: string, date?: string) => unwrapArray<SecurityEventRow>(await request<unknown>(`/api/sites/${siteId}/security-events${date ? `?date=${date}` : ''}`)),
  reviewSecurityEvent: (id: string, body: { status: SecurityEventStatus; reviewNote?: string }) => request(`/api/security-events/${id}/review`, { method: 'PATCH', body: JSON.stringify(body) }),
  linkSecurityEventToWorker: (id: string, body: { workerId: string; deviceId?: string; remapRecent?: boolean; reviewNote?: string }) =>
    request(`/api/security-events/${id}/link-worker`, { method: 'POST', body: JSON.stringify(body) }),
  securitySnapshotUrl: (snapshotUrl?: string) => withApiBase(snapshotUrl),
  attendanceSnapshotUrl: (snapshotUrl?: string) => withApiBase(snapshotUrl),
  getListenerStatus: () => request<ListenerStatus>('/api/dahua/listener/status'),
  getActiveRegisterStatus: () => request<ActiveRegisterStatus>('/api/dahua/active-register/status'),
  getActiveRegisterRawEvents: async (limit = 100) => unwrapArray<ActiveRegisterRawEventRow>(await request<unknown>(`/api/dahua/active-register/raw-events?limit=${limit}`)),
  getAdminLicenses: async () => unwrapArray<AdminTenantLicenseRow>(await request<unknown>('/api/admin/licenses')),
  createAdminLicense: (body: { tenantId: string; plan: LicensePlan; expiresAt?: string; maxProjects?: number; maxUsers?: number; maxCameras?: number }) =>
    request<CreateAdminLicenseResponse>('/api/admin/licenses', { method: 'POST', body: JSON.stringify(body) }),
  activateTenantLicense: (tenantId: string, licenseId?: string) =>
    request<LicenseResponse>(`/api/admin/licenses/${tenantId}/activate`, { method: 'POST', body: JSON.stringify({ licenseId }) }),
}


