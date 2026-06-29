export type SalaryType = 'Saatlıq' | 'Günlük' | 'Aylıq'
export type WorkerStatus = 'Aktiv' | 'Passiv'
export type AttendanceStatus = 'Gəlib' | 'Gəlməyib' | 'Gecikib' | 'Erkən çıxıb' | 'İcazəli'
export type EntryMethod = 'Mobil App' | 'Turniket' | 'Prorab Tablet' | 'Manual' | 'Offline'
export type RiskLevel = 'Aşağı' | 'Orta' | 'Yüksək' | 'Kritik'
export type ExportStatus = 'Hazır' | 'Xəta' | 'Xəbərdarlıq' | 'Göndərilib'

export interface Company {
  company_name: string
  voen: string
  contact_person: string
  phone: string
}

export interface Site {
  site_id: string
  site_name: string
  address: string
  latitude: number
  longitude: number
  radius_m: number
  work_start: string
  work_end: string
}

export interface Worker {
  worker_id: string
  full_name: string
  phone: string
  position: string
  brigade: string
  salary_type: SalaryType
  salary_rate: number
  status: WorkerStatus
}

export interface Assignment {
  id?: number
  worker_id: string
  site_id: string
  start_date: string
  end_date: string
  active: boolean
}

export interface WorkPhase {
  phase_id: string
  site_id: string
  cost_code: string
  phase_name: string
  planned_hours: number
  planned_quantity: number
  unit: string
}

export interface AttendanceRecord {
  attendance_id: string
  worker_id: string
  site_id: string
  date: string
  planned_check_in: string
  actual_check_in: string
  planned_check_out: string
  actual_check_out: string
  status: AttendanceStatus
  late_minutes: number
  early_leave_minutes: number
  worked_hours: number
  overtime_hours: number
  entry_method: EntryMethod
  is_offline: boolean
  sync_time: string
  risk_score: number
  risk_level: RiskLevel
  approved_by: string
}

export interface RiskRecord {
  risk_id: string
  worker_id: string
  site_id: string
  date: string
  risk_type: string
  risk_reason: string
  risk_score: number
  risk_level: RiskLevel
  repeat_count: number
  entry_method: EntryMethod
  recommendation: string
}

export interface PayrollRecord {
  payroll_id: string
  worker_id: string
  site_id: string
  month: string
  salary_type: SalaryType
  salary_rate: number
  normal_hours: number
  overtime_hours: number
  permission_hours: number
  risky_hours: number
  approved_hours: number
  gross_amount: number
  adjustment: number
  net_amount: number
  export_status: ExportStatus
}

export interface SupervisorAuditRecord {
  supervisor_id: string
  supervisor_name: string
  site_id: string
  period: string
  tablet_entries: number
  manual_edits: number
  checkin_changes: number
  checkout_changes: number
  risky_approvals: number
  repeated_worker_confirmations: number
  late_approvals: number
  audit_status: 'Uyğun' | 'Yaxşı' | 'Nəzarət tələb edir' | 'Riskli'
}

export interface CostCodeRecord {
  id?: number
  site_id: string
  cost_code: string
  phase_name: string
  brigade: string
  planned_hours: number
  actual_hours: number
  hour_difference: number
  planned_quantity: number
  actual_quantity: number
  unit: string
  productivity_percent: number
  labor_cost: number
  status: 'Yaxşı' | 'Orta' | 'Təkmilləşdirilməlidir'
}

export interface ImportedBaseData {
  company: Company[]
  sites: Site[]
  workers: Worker[]
  assignments: Assignment[]
  workPhases: WorkPhase[]
}

export interface BuildTrackData extends ImportedBaseData {
  attendanceRecords: AttendanceRecord[]
  riskRecords: RiskRecord[]
  payrollRecords: PayrollRecord[]
  supervisorAuditRecords: SupervisorAuditRecord[]
  costCodeRecords: CostCodeRecord[]
  generatedAt: string
  source: 'sample' | 'imported'
}
