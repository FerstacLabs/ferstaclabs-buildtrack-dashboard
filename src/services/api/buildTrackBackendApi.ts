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

export interface AttendanceDailyRosterRow {
  key: string
  workerId: string
  workerExternalId: string
  workerName: string
  siteId: string
  siteName: string
  role?: string
  brigade?: string
  plannedCheckIn: string
  plannedCheckOut: string
  actualCheckIn?: string
  actualCheckInLocal?: string
  actualCheckOut?: string
  actualCheckOutLocal?: string
  status: 'Gəlib' | 'Gecikib' | 'Gəlməyib' | 'Riskli'
  lateMinutes: number
  earlyExitMinutes: number
  workedHours: number
  entryMethod: string
  riskScore: number
  riskLevel: 'Aşağı' | 'Orta' | 'Yüksək' | 'Kritik'
  source?: string
}

export interface AttendanceDailyRosterReport {
  workDate: string
  plannedStart: string
  plannedEnd: string
  lateGraceMinutes: number
  earlyExitGraceMinutes: number
  activeWorkersCount: number
  presentCount: number
  absentCount: number
  lateCount: number
  earlyExitCount: number
  totalWorkedHours: number
  attendancePercent: number
  rows: AttendanceDailyRosterRow[]
}

export interface AttendanceDisciplineRow {
  key: string
  workerId: string
  workerExternalId: string
  workerName: string
  siteId: string
  siteName: string
  role?: string
  brigade?: string
  scheduledDays: number
  presentDays: number
  absentDays: number
  lateCount: number
  totalLateMinutes: number
  earlyExitCount: number
  totalEarlyExitMinutes: number
  approvedPermissionDays: number
  approvedPermissionHours: number
  attendancePercent: number
  riskScore: number
  riskLevel: string
  trend: string
  note: string
}

export interface AttendanceDisciplineTrendPoint {
  key: string
  date: string
  label: string
  lateCount: number
  totalLateMinutes: number
  lateHours: number
  earlyExitCount: number
}

export interface AttendanceDisciplineReport {
  dateFrom: string
  dateTo: string
  plannedStart: string
  plannedEnd: string
  lateGraceMinutes: number
  earlyExitGraceMinutes: number
  scheduledWorkerDays: number
  presentWorkerDays: number
  absentWorkerDays: number
  lateCount: number
  totalLateMinutes: number
  earlyExitCount: number
  totalEarlyExitMinutes: number
  approvedPermissionDays: number
  approvedPermissionHours: number
  attendancePercent: number
  permissionDomainAvailable: boolean
  rows: AttendanceDisciplineRow[]
  trend: AttendanceDisciplineTrendPoint[]
}

export type SecurityEventStatus = 'Open' | 'PendingCorrelation' | 'Reviewed' | 'Ignored' | 'AutoResolved'

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

export type FieldDailyReportStatus = 'Draft' | 'Submitted' | 'Approved' | 'NeedsCorrection' | 'Rejected'
export type FieldWarehouseRequestStatus = 'Draft' | 'Submitted' | 'UnderReview' | 'NeedsJustification' | 'PendingApproval' | 'Approved' | 'PartiallyApproved' | 'Rejected' | 'InFulfillment' | 'ReadyForPickup' | 'Issued' | 'Closed' | 'Cancelled'
export type FieldWorkerEventType = 'Late' | 'LeftEarly' | 'Absent' | 'Permission' | 'Medical' | 'SiteTransfer' | 'SafetyWarning' | 'ManualAttendanceCorrectionRequest' | 'Other'
export type FieldSiteNoteCategory = 'Weather' | 'MaterialDelay' | 'Equipment' | 'Labor' | 'Safety' | 'Quality' | 'Access' | 'Other'

export interface FieldAssignment {
  id?: string
  siteId: string
  siteName: string
  address?: string
  assignedAt?: string
  projectId?: string
  isActive?: boolean
  validFrom?: string
  validUntil?: string
}

export interface FieldMe {
  userId: string
  fullName: string
  email: string
  role: string
  tenantId: string
  tenantName: string
  assignments: FieldAssignment[]
}

interface FieldAssignmentApiResponse {
  id: string
  siteId: string
  siteName: string
  siteAddress?: string
  projectId?: string
  isActive: boolean
  validFrom?: string
  validUntil?: string
}

interface FieldMeApiResponse {
  user: {
    id: string
    tenantId: string
    fullName: string
    email: string
    role: string
    status: string
  }
  tenant: {
    id: string
    companyName: string
    code?: string
    status: string
  }
  assignments: FieldAssignmentApiResponse[]
}

export interface FieldDashboard {
  siteId: string
  siteName: string
  workDate: string
  activeWorkers: number
  todayReports: number
  pendingReports: number
  openWarehouseRequests: number
  workerRiskEvents: number
  recentActivity: FieldActivity[]
}

export interface FieldActivity {
  type: string
  title: string
  timestamp: string
  status?: string
}

export interface FieldSmetaItem {
  id: string
  siteId: string
  stageName: string
  workName: string
  unit: string
  workCategory?: string
}

export interface FieldDailyReportLine {
  id: string
  smetaItemId: string
  stageName: string
  workName: string
  unit: string
  reportedQuantity: number
  workerCount?: number
  workHours?: number
  note?: string
}

export interface FieldDailyReport {
  id: string
  siteId: string
  siteName: string
  supervisorUserId: string
  supervisorName: string
  reportDate: string
  status: FieldDailyReportStatus
  weather?: string
  weatherCondition?: string
  generalNote?: string
  submittedAt?: string
  reviewedAt?: string
  reviewedByUserId?: string
  reviewedByName?: string
  reviewNote?: string
  createdAt: string
  updatedAt?: string
  lines: FieldDailyReportLine[]
}

export interface SaveFieldDailyReportBody {
  id?: string
  siteId: string
  reportDate: string
  weatherCondition?: string
  generalNote?: string
  lines: {
    id?: string
    smetaItemId: string
    reportedQuantity: number
    workerCount: number
    workHours: number
    note?: string
  }[]
}

export interface FieldWorker {
  id: string
  siteId: string
  externalWorkerCode: string
  fullName: string
  brigade?: string
  role?: string
  todayStatus: string
  firstSeenAt?: string
  lastSeenAt?: string
  workedMinutesToday: number
  riskScore: number
}

export interface FieldWorkerEvent {
  id: string
  siteId: string
  workerId: string
  workerName: string
  supervisorUserId: string
  supervisorName: string
  eventType: FieldWorkerEventType
  eventDateTime: string
  reason: string
  riskDelta: number
  status: 'Submitted' | 'Reviewed' | 'Rejected'
  createdAt: string
}

export interface FieldSiteNote {
  id: string
  siteId: string
  siteName: string
  supervisorUserId: string
  supervisorName: string
  eventDateTime: string
  category: FieldSiteNoteCategory
  text: string
  createdAt: string
}

export interface FieldWarehouseCatalogItem {
  id: string
  name: string
  nameAz?: string
  nameRu?: string
  nameEn?: string
  category?: string
  subcategory?: string
  unit: string
  code?: string
  itemType?: string
  searchAliases?: string
}

export interface FieldWarehouseRequestLine {
  id: string
  catalogItemId: string
  itemName: string
  category: string
  requestedQuantity: number
  unit: string
  reason?: string
  status: string
}

export interface FieldWarehouseRequest {
  id: string
  code?: string
  siteId: string
  siteName: string
  supervisorUserId: string
  supervisorName: string
  catalogItemId: string
  itemName?: string
  materialName: string
  unit: string
  requestedQuantity: number
  reason: string
  justificationRequestNote?: string
  generalNote?: string
  justification?: string
  urgency: 'Normal' | 'Urgent' | 'Critical'
  status: FieldWarehouseRequestStatus
  managerNote?: string
  managerComment?: string
  abnormalRequest?: boolean
  lines?: FieldWarehouseRequestLine[]
  createdAt: string
  updatedAt?: string
}

export interface ManagementWarehouseLine {
  id: string
  catalogItemId: string
  itemName: string
  code?: string
  category: string
  requestedQuantity: number
  approvedQuantity: number
  reservedQuantity: number
  issuedQuantity: number
  onHandQuantity: number
  availableQuantity: number
  shortfallQuantity: number
  unit: string
  reason?: string
  status: string
}

export interface ManagementWarehouseRequest {
  id: string
  code: string
  siteId: string
  siteName?: string
  supervisorUserId: string
  supervisorName?: string
  neededBy?: string
  urgency: 'Normal' | 'Urgent' | 'Critical'
  status: FieldWarehouseRequestStatus
  generalNote?: string
  justificationRequestNote?: string
  justification?: string
  managerComment?: string
  abnormalRequest: boolean
  totalRequested: number
  totalReserved: number
  totalShortfall: number
  createdAt: string
  updatedAt?: string
  lines: ManagementWarehouseLine[]
}

export interface WarehouseStockItem {
  catalogItemId: string
  itemName: string
  category: string
  subcategory?: string
  unit: string
  code?: string
  onHandQuantity: number
  reservedQuantity: number
  availableQuantity: number
  issuedQuantity: number
  minimumQuantity: number
  stockStatus: string
}

export interface ProcurementNeed {
  id: string
  sourceRequestId: string
  sourceRequestLineId: string
  catalogItemId: string
  itemName: string
  category: string
  requiredQuantity: number
  alreadyAvailableQuantity: number
  shortfallQuantity: number
  purchasedQuantity: number
  receivedQuantity: number
  unit: string
  priority: 'Normal' | 'Urgent' | 'Critical'
  requiredBy?: string
  status: string
  reason: string
  createdAt: string
}

export interface ProcurementTaskLine {
  id: string
  procurementNeedId: string
  catalogItemId: string
  itemName: string
  category: string
  requestedQuantity: number
  purchasedQuantity: number
  acceptedQuantity: number
  unit: string
  status: string
  note?: string
  unitPrice?: number
  supplierId?: string
  supplierName?: string
}

export interface ProcurementAttachment {
  id: string
  taskId: string
  taskLineId?: string
  attachmentType: 'ProductPhoto' | 'Receipt' | 'Invoice' | 'DeliveryNote' | 'Other'
  originalFileName: string
  mimeType: string
  size: number
  createdAt: string
  downloadUrl: string
}

export interface ProcurementTask {
  id: string
  code: string
  assignedProcurementUserId?: string
  assignedProcurementUserName?: string
  status: string
  priority: 'Normal' | 'Urgent' | 'Critical'
  requiredBy?: string
  managerInstruction?: string
  createdAt: string
  assignedAt?: string
  startedAt?: string
  submittedAt?: string
  verifiedAt?: string
  verificationNote?: string
  lines: ProcurementTaskLine[]
  attachments: ProcurementAttachment[]
}

export interface SupplierRow {
  id: string
  name: string
  taxId?: string
  phone?: string
  email?: string
  address?: string
  contactPerson?: string
  categories?: string
  status: string
  notes?: string
}

export interface ProcurementAgent {
  id: string
  fullName: string
  email: string
  phone?: string
  status: 'Active' | 'Disabled'
  openTasks: number
  lastLoginAt?: string
}

export interface SupplyDashboard {
  assignedTasks: number
  shoppingTasks: number
  submittedTasks: number
  unreadNotifications: number
  recentTasks: ProcurementTask[]
}

export interface SupplyNotification {
  id: string
  audience: string
  title: string
  message: string
  referenceType?: string
  referenceId?: string
  status: 'Unread' | 'Read'
  createdAt: string
}

export interface SupervisorSummary {
  id: string
  fullName: string
  email: string
  phone?: string
  status: 'Active' | 'Disabled'
  lastLoginAt?: string
  assignments: FieldAssignment[]
  pendingReports: number
  openWarehouseRequests: number
  recentAuditEvents: number
}

export interface SupervisorAuditEventRow {
  id: string
  siteId?: string
  siteName?: string
  supervisorUserId?: string
  supervisorName?: string
  action?: string
  eventType?: string
  entityType?: string
  entityId?: string
  requiresManagerReview: boolean
  message?: string
  timestamp: string
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

const objectValue = (payload: unknown): Record<string, unknown> =>
  payload && typeof payload === 'object' ? payload as Record<string, unknown> : {}

const numericAlias = (payload: Record<string, unknown>, names: string[], fallback = 0) => {
  for (const name of names) {
    const value = payload[name]
    if (typeof value === 'number' && Number.isFinite(value)) return value
    if (typeof value === 'string' && value.trim() && Number.isFinite(Number(value))) return Number(value)
  }
  return fallback
}

const arrayAlias = <T>(payload: Record<string, unknown>, names: string[]) => {
  for (const name of names) {
    const value = payload[name]
    if (Array.isArray(value)) return value as T[]
  }
  return []
}

const normalizeLiveAttendanceApiResponse = (payload: unknown) => objectValue(unwrapValue<unknown>(payload))

const normalizeAttendanceLiveStatus = (payload: unknown): AttendanceLiveStatus => {
  const value = normalizeLiveAttendanceApiResponse(payload)
  const workers = arrayAlias<AttendanceLiveWorker>(value, ['workers', 'sessions', 'rows', 'items'])
  return {
    workDate: typeof value.workDate === 'string' ? value.workDate : undefined,
    activeWorkersCount: numericAlias(value, ['activeWorkersCount', 'activeWorkers'], workers.length),
    workers,
    staleOpenSessionsCount: numericAlias(value, ['staleOpenSessionsCount'], 0),
  }
}

const normalizeAttendanceDaily = (payload: unknown): AttendanceDailySummary => {
  const value = normalizeLiveAttendanceApiResponse(payload)
  const sessions = arrayAlias<AttendanceSessionRow>(value, ['sessions', 'workers', 'rows', 'items'])
  const activeFromRows = sessions.filter((session) => session.status === 'Open' || !session.checkOutTime).length
  const closedFromRows = sessions.filter((session) => session.status === 'Closed' || Boolean(session.checkOutTime)).length
  return {
    workDate: typeof value.workDate === 'string' ? value.workDate : '',
    totalWorkersCheckedIn: numericAlias(value, ['totalWorkersCheckedIn', 'todaySeen', 'checkedInWorkers'], sessions.length),
    activeWorkersCount: numericAlias(value, ['activeWorkersCount', 'activeWorkers'], activeFromRows),
    closedSessionsCount: numericAlias(value, ['closedSessionsCount', 'confirmedCheckouts'], closedFromRows),
    totalWorkedHours: numericAlias(value, ['totalWorkedHours', 'totalHours'], 0),
    sessions,
  }
}

const normalizeFieldAssignment = (assignment: FieldAssignmentApiResponse): FieldAssignment => ({
  id: assignment.id,
  siteId: assignment.siteId,
  siteName: assignment.siteName,
  address: assignment.siteAddress,
  assignedAt: assignment.validFrom,
  projectId: assignment.projectId,
  isActive: assignment.isActive,
  validFrom: assignment.validFrom,
  validUntil: assignment.validUntil,
})

const normalizeFieldMe = (response: FieldMeApiResponse): FieldMe => ({
  userId: response.user.id,
  fullName: response.user.fullName,
  email: response.user.email,
  role: response.user.role,
  tenantId: response.tenant.id,
  tenantName: response.tenant.companyName,
  assignments: response.assignments.map(normalizeFieldAssignment),
})

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
  getAttendanceDailyRoster: (params?: { siteId?: string; date?: string }) => {
    const query = new URLSearchParams()
    if (params?.siteId) query.set('siteId', params.siteId)
    if (params?.date) query.set('date', params.date)
    return request<AttendanceDailyRosterReport>(`/api/attendance/daily-roster${query.toString() ? `?${query}` : ''}`)
  },
  getAttendanceDiscipline: (params?: { siteId?: string; dateFrom?: string; dateTo?: string }) => {
    const query = new URLSearchParams()
    if (params?.siteId) query.set('siteId', params.siteId)
    if (params?.dateFrom) query.set('dateFrom', params.dateFrom)
    if (params?.dateTo) query.set('dateTo', params.dateTo)
    return request<AttendanceDisciplineReport>(`/api/attendance/discipline${query.toString() ? `?${query}` : ''}`)
  },
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
  getFieldMe: async () => normalizeFieldMe(await request<FieldMeApiResponse>('/api/field/me')),
  getFieldAssignments: async () => unwrapArray<FieldAssignmentApiResponse>(await request<unknown>('/api/field/assignments')).map(normalizeFieldAssignment),
  getFieldDashboard: (siteId?: string) => request<FieldDashboard>(`/api/field/dashboard${siteId ? `?siteId=${encodeURIComponent(siteId)}` : ''}`),
  getFieldSmetaItems: async (siteId: string) => unwrapArray<FieldSmetaItem>(await request<unknown>(`/api/field/smeta-items?siteId=${encodeURIComponent(siteId)}`)),
  getFieldWorkers: async (siteId: string) => unwrapArray<FieldWorker>(await request<unknown>(`/api/field/workers?siteId=${encodeURIComponent(siteId)}`)),
  getFieldDailyReports: async (siteId?: string) => unwrapArray<FieldDailyReport>(await request<unknown>(`/api/field/daily-reports${siteId ? `?siteId=${encodeURIComponent(siteId)}` : ''}`)),
  saveFieldDailyReport: (body: SaveFieldDailyReportBody) => request<FieldDailyReport>('/api/field/daily-reports', { method: 'POST', body: JSON.stringify(body) }),
  updateFieldDailyReport: (id: string, body: SaveFieldDailyReportBody) => request<FieldDailyReport>(`/api/field/daily-reports/${id}`, { method: 'PUT', body: JSON.stringify(body) }),
  submitFieldDailyReport: (id: string) => request<FieldDailyReport>(`/api/field/daily-reports/${id}/submit`, { method: 'POST' }),
  getFieldSiteNotes: async (siteId?: string) => unwrapArray<FieldSiteNote>(await request<unknown>(`/api/field/site-notes${siteId ? `?siteId=${encodeURIComponent(siteId)}` : ''}`)),
  createFieldSiteNote: (body: { siteId: string; category: FieldSiteNoteCategory; text: string; eventDateTime?: string }) =>
    request<FieldSiteNote>('/api/field/site-notes', { method: 'POST', body: JSON.stringify(body) }),
  getFieldWorkerEvents: async (siteId?: string) => unwrapArray<FieldWorkerEvent>(await request<unknown>(`/api/field/worker-events${siteId ? `?siteId=${encodeURIComponent(siteId)}` : ''}`)),
  createFieldWorkerEvent: (body: { siteId: string; workerId: string; eventType: FieldWorkerEventType; eventDateTime?: string; reason: string }) =>
    request<FieldWorkerEvent>('/api/field/worker-events', { method: 'POST', body: JSON.stringify(body) }),
  getFieldWarehouseCatalog: async () => unwrapArray<FieldWarehouseCatalogItem>(await request<unknown>('/api/field/warehouse/catalog')),
  searchCatalogItems: async (params?: { q?: string; category?: string; subcategory?: string; limit?: number }) => {
    const query = new URLSearchParams()
    if (params?.q) query.set('q', params.q)
    if (params?.category) query.set('category', params.category)
    if (params?.subcategory) query.set('subcategory', params.subcategory)
    if (params?.limit) query.set('limit', String(params.limit))
    return unwrapArray<FieldWarehouseCatalogItem>(await request<unknown>(`/api/catalog/items/search${query.toString() ? `?${query}` : ''}`))
  },
  getFieldWarehouseRequests: async (siteId?: string) => unwrapArray<FieldWarehouseRequest>(await request<unknown>(`/api/field/warehouse/requests${siteId ? `?siteId=${encodeURIComponent(siteId)}` : ''}`)),
  createFieldWarehouseRequest: (body: { siteId: string; catalogItemId: string; requestedQuantity: number; neededBy?: string; reason: string; justification?: string; urgency: 'Normal' | 'Urgent' | 'Critical' }) =>
    request<FieldWarehouseRequest>('/api/field/warehouse/requests', { method: 'POST', body: JSON.stringify(body) }),
  createFieldWarehouseCartRequest: (body: { siteId: string; neededBy?: string; urgency: 'Normal' | 'Urgent' | 'Critical'; generalNote?: string; lines: { catalogItemId: string; requestedQuantity: number; reason?: string; specificationJson?: string }[] }) =>
    request<FieldWarehouseRequest>('/api/field/warehouse/cart-requests', { method: 'POST', body: JSON.stringify(body) }),
  submitFieldWarehouseJustification: (id: string, justification: string) =>
    request<FieldWarehouseRequest>(`/api/field/warehouse/requests/${id}/justification`, { method: 'POST', body: JSON.stringify({ justification }) }),
  getWarehouseStock: async () => unwrapArray<WarehouseStockItem>(await request<unknown>('/api/procurement/warehouse/stock')),
  getProcurementWarehouseRequests: async (siteId?: string) => unwrapArray<ManagementWarehouseRequest>(await request<unknown>(`/api/procurement/warehouse/requests${siteId ? `?siteId=${encodeURIComponent(siteId)}` : ''}`)),
  approveProcurementWarehouseRequest: (id: string, managerComment?: string) =>
    request(`/api/procurement/warehouse/requests/${id}/approve`, { method: 'POST', body: JSON.stringify({ managerComment }) }),
  issueProcurementWarehouseRequest: (id: string, body: { recipientName?: string; handoverNote?: string; warehouseId?: string }) =>
    request(`/api/procurement/warehouse/requests/${id}/issue`, { method: 'POST', body: JSON.stringify(body) }),
  getProcurementNeeds: async () => unwrapArray<ProcurementNeed>(await request<unknown>('/api/procurement/needs')),
  getProcurementTasks: async () => unwrapArray<ProcurementTask>(await request<unknown>('/api/procurement/tasks')),
  getProcurementTask: (id: string) => request<ProcurementTask>(`/api/procurement/tasks/${id}`),
  createProcurementTask: (body: { needIds: string[]; assignedProcurementUserId?: string; managerInstruction?: string }) =>
    request<ProcurementTask>('/api/procurement/tasks', { method: 'POST', body: JSON.stringify(body) }),
  verifyProcurementTask: (id: string, verificationNote?: string) =>
    request<ProcurementTask>(`/api/procurement/tasks/${id}/verify`, { method: 'POST', body: JSON.stringify({ verificationNote }) }),
  returnProcurementTaskForCorrection: (id: string, note?: string) =>
    request<ProcurementTask>(`/api/procurement/tasks/${id}/return-correction`, { method: 'POST', body: JSON.stringify({ note }) }),
  createGoodsReceipt: (body: { taskId: string; warehouseId?: string; note?: string }) =>
    request(`/api/procurement/goods-receipts`, { method: 'POST', body: JSON.stringify(body) }),
  getSuppliers: async () => unwrapArray<SupplierRow>(await request<unknown>('/api/procurement/suppliers')),
  saveSupplier: (body: Partial<SupplierRow> & { name: string }) => request<SupplierRow>('/api/procurement/suppliers', { method: 'POST', body: JSON.stringify(body) }),
  getProcurementAgents: async () => unwrapArray<ProcurementAgent>(await request<unknown>('/api/procurement/agents')),
  createProcurementAgent: (body: { fullName: string; email: string; phone?: string; temporaryPassword: string }) =>
    request<ProcurementAgent>('/api/procurement/agents', { method: 'POST', body: JSON.stringify(body) }),
  updateProcurementAgent: (id: string, body: { fullName: string; phone?: string; status: 'Active' | 'Disabled' }) =>
    request<void>(`/api/procurement/agents/${id}`, { method: 'PUT', body: JSON.stringify(body) }),
  resetProcurementAgentPassword: (id: string, temporaryPassword: string) =>
    request<void>(`/api/procurement/agents/${id}/reset-password`, { method: 'POST', body: JSON.stringify({ temporaryPassword }) }),
  getProcurementTrace: (fieldRequestId: string) => request(`/api/procurement/trace/${fieldRequestId}`),
  getSupplyMe: () => request('/api/supply/me'),
  getSupplyDashboard: () => request<SupplyDashboard>('/api/supply/dashboard'),
  getSupplyTasks: async () => unwrapArray<ProcurementTask>(await request<unknown>('/api/supply/tasks')),
  getSupplyTask: (id: string) => request<ProcurementTask>(`/api/supply/tasks/${id}`),
  acceptSupplyTask: (id: string) => request<ProcurementTask>(`/api/supply/tasks/${id}/accept`, { method: 'POST' }),
  startSupplyTask: (id: string) => request<ProcurementTask>(`/api/supply/tasks/${id}/start`, { method: 'POST' }),
  updateSupplyTaskLinePurchase: (taskId: string, lineId: string, body: { purchasedQuantity: number; unitPrice?: number; supplierId?: string; note?: string }) =>
    request(`/api/supply/tasks/${taskId}/lines/${lineId}/purchase`, { method: 'POST', body: JSON.stringify(body) }),
  uploadSupplyTaskAttachment: (taskId: string, formData: FormData) => uploadRequest<ProcurementAttachment>(`/api/supply/tasks/${taskId}/attachments`, formData),
  supplyAttachmentUrl: (downloadUrl?: string) => withApiBase(downloadUrl),
  submitSupplyTask: (id: string) => request<ProcurementTask>(`/api/supply/tasks/${id}/submit`, { method: 'POST' }),
  getSupplyNotifications: async () => unwrapArray<SupplyNotification>(await request<unknown>('/api/supply/notifications')),
  getSupplySettings: () => request('/api/supply/settings'),
  getSupervisors: async () => unwrapArray<SupervisorSummary>(await request<unknown>('/api/supervisors')),
  createSupervisor: (body: { fullName: string; email: string; phone?: string; password: string; siteIds: string[] }) =>
    request<SupervisorSummary>('/api/supervisors', {
      method: 'POST',
      body: JSON.stringify({
        fullName: body.fullName,
        email: body.email,
        phone: body.phone,
        temporaryPassword: body.password,
        siteIds: body.siteIds,
      }),
    }),
  updateSupervisor: (id: string, body: { fullName: string; phone?: string; siteIds: string[]; status: 'Active' | 'Disabled' }) =>
    request<SupervisorSummary>(`/api/supervisors/${id}`, { method: 'PUT', body: JSON.stringify(body) }),
  resetSupervisorPassword: (id: string, password: string) =>
    request<void>(`/api/supervisors/${id}/reset-password`, { method: 'POST', body: JSON.stringify({ temporaryPassword: password }) }),
  suspendSupervisor: (id: string) => request<void>(`/api/supervisors/${id}/suspend`, { method: 'POST' }),
  reactivateSupervisor: (id: string) => request<void>(`/api/supervisors/${id}/reactivate`, { method: 'POST' }),
  getManagementFieldReports: async (siteId?: string) => unwrapArray<FieldDailyReport>(await request<unknown>(`/api/management/field-reports${siteId ? `?siteId=${encodeURIComponent(siteId)}` : ''}`)),
  reviewManagementFieldReport: (id: string, body: { status: FieldDailyReportStatus; reviewNote?: string }) =>
    request<FieldDailyReport>(`/api/management/field-reports/${id}/review`, { method: 'POST', body: JSON.stringify(body) }),
  getManagementWarehouseRequests: async (siteId?: string) => unwrapArray<FieldWarehouseRequest>(await request<unknown>(`/api/management/field-warehouse-requests${siteId ? `?siteId=${encodeURIComponent(siteId)}` : ''}`)),
  reviewManagementWarehouseRequest: (id: string, body: { status: FieldWarehouseRequestStatus; managerNote?: string; managerComment?: string; approvedQuantity?: number }) =>
    request<FieldWarehouseRequest>(`/api/management/field-warehouse-requests/${id}/review`, { method: 'POST', body: JSON.stringify({ ...body, managerComment: body.managerComment ?? body.managerNote }) }),
  getSupervisorAuditEvents: async (siteId?: string) => unwrapArray<SupervisorAuditEventRow>(await request<unknown>(`/api/supervisor-audit/events${siteId ? `?siteId=${encodeURIComponent(siteId)}` : ''}`)),
  getAdminLicenses: async () => unwrapArray<AdminTenantLicenseRow>(await request<unknown>('/api/admin/licenses')),
  createAdminLicense: (body: { tenantId: string; plan: LicensePlan; expiresAt?: string; maxProjects?: number; maxUsers?: number; maxCameras?: number }) =>
    request<CreateAdminLicenseResponse>('/api/admin/licenses', { method: 'POST', body: JSON.stringify(body) }),
  activateTenantLicense: (tenantId: string, licenseId?: string) =>
    request<LicenseResponse>(`/api/admin/licenses/${tenantId}/activate`, { method: 'POST', body: JSON.stringify({ licenseId }) }),
}

const uploadRequest = async <T>(path: string, body: FormData): Promise<T> => {
  const url = `${API_BASE}${path}`
  const headers = new Headers()
  Object.entries(authHeader()).forEach(([key, value]) => headers.set(key, value))
  const response = await fetch(url, { method: 'POST', headers, body })
  const text = response.status === 204 ? '' : await response.text()
  const parsed = parseJsonBody(text)
  if (!response.ok) {
    console.error('BuildTrack backend upload failed', { url, status: response.status, parsed })
    throw new BackendApiError(typeof parsed === 'string' ? parsed : JSON.stringify(parsed ?? ''), url, response.status, text)
  }
  return parsed as T
}


