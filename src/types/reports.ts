import type {
  AttendanceStatus,
  EntryMethod,
  ExportStatus,
  RiskLevel,
  SalaryType,
} from './models'

export interface ReportFilters {
  dateRange: [string, string]
  siteId: string
  brigade: string
  status: string
  position: string
  riskLevel: string
  entryMethod: string
  exportStatus: string
  month: string
  supervisor: string
  reportType: string
}

export interface KpiView {
  title: string
  value: string
  suffix?: string
  trend?: string
  tone: 'blue' | 'green' | 'orange' | 'red' | 'purple'
}

export interface ChartPoint {
  name: string
  value?: number
  plan?: number
  faktiki?: number
  saat?: number
  davamiyyət?: number
  gecikmə?: number
  erkən?: number
  export?: number
  xərc?: number
}

export interface DailyAttendanceRow {
  key: string
  worker_id: string
  full_name: string
  site_name: string
  position: string
  brigade: string
  planned_check_in: string
  actual_check_in: string
  planned_check_out: string
  actual_check_out: string
  status: AttendanceStatus
  late_minutes: number
  worked_hours: number
  entry_method: EntryMethod
  risk_score: number
  risk_level: RiskLevel
}

export interface SiteHoursRow {
  key: string
  site_name: string
  planned_workers: number
  actual_workers: number
  absent_workers: number
  normal_hours: number
  overtime_hours: number
  risky_hours: number
  auto_geofence: number
  labor_cost: number
  execution_percent: number
}

export interface RiskWorkerRow {
  key: string
  full_name: string
  site_name: string
  position: string
  risk_score: number
  risk_level: RiskLevel
  risk_reason: string
  repeat_count: number
  date: string
  entry_method: EntryMethod
  approved_by: string
  recommendation: string
}

export interface DelayPermissionRow {
  key: string
  full_name: string
  site_name: string
  position: string
  late_count: number
  late_minutes: number
  early_count: number
  permission_hours: number
  attendance_percent: number
  trend: 'up' | 'stable' | 'down'
  note: string
}

export interface PayrollRow {
  key: string
  full_name: string
  site_name: string
  position: string
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

export interface PerformanceRow {
  key: string
  full_name: string
  position: string
  site_brigade: string
  period: string
  attendance_percent: number
  average_late: number
  risk_events: number
  total_hours: number
  overtime_hours: number
  raise_count: number
  last_raise: string
  current_rate: number
  performance_status: 'Yüksək' | 'Orta' | 'Aşağı' | 'Zəif'
  recommendation: string
}

export interface ExportValidationRow {
  key: string
  row_id: string
  full_name: string
  site_name: string
  cost_code: string
  salary_type: SalaryType
  approved_hours: number
  net_amount: number
  account_code: string
  export_status: ExportStatus
  error_message: string
  checked_at: string
}

export interface CustomReportRow {
  key: string
  name: string
  category: string
  report_type: string
  site_id: string
  brigade: string
  created_at: string
  updated_at: string
  filter_count: number
  column_count: number
  export_format: string
  owner: string
  status: 'Aktiv' | 'Qaralama'
  last_used: string
}

export interface TimelineEvent {
  id: string
  date: string
  title: string
  subtitle: string
  tone: 'blue' | 'green' | 'orange' | 'red'
}
