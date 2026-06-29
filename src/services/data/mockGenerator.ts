import dayjs from 'dayjs'
import type {
  Assignment,
  AttendanceRecord,
  BuildTrackData,
  Company,
  CostCodeRecord,
  EntryMethod,
  ImportedBaseData,
  PayrollRecord,
  RiskRecord,
  SalaryType,
  Site,
  SupervisorAuditRecord,
  Worker,
  WorkPhase,
} from '../../types/models'
import { DEFAULT_END_DATE, DEFAULT_MONTH, DEFAULT_START_DATE, getDatesInRange } from '../../utils/dateUtils'
import { calculateRiskLevel, riskRecommendation } from '../../utils/riskUtils'

const names = [
  'Rəşad İsmayılov',
  'Vüqar Hüseynov',
  'Elvin Həsənov',
  'Samir Mustafayev',
  'Tural Rzayev',
  'Natiq Məmmədli',
  'İlqar Əliyev',
  'Orxan Quliyev',
  'Fərid Qədirov',
  'Mahir Abbasov',
  'Rauf Məmmədov',
  'Sənan Mustafayev',
  'Bəxtiyar Əhmədov',
  'Aydın Quliyev',
  'Zeynəb Əlizadə',
  'Günel Səfərova',
]

const positions = [
  'Betonçu',
  'Armaturçu',
  'Santexnik',
  'Elektrik',
  'Qaynaqçı',
  'Suvaqçı',
  'Qəlib ustası',
  'Köməkçi işçi',
  'Dəmirçi',
  'Anbardar',
]

const brigades = ['A', 'B', 'C', 'D', 'E', 'F', 'G', 'H']

const supervisors = [
  'Rəşad İ.',
  'Samir M.',
  'Tural R.',
  'Vüqar H.',
  'İlqar Q.',
  'Natiq M.',
  'Orxan N.',
  'Mübariz A.',
]

const defaultSites: Site[] = [
  {
    site_id: 'S-001',
    site_name: 'Sea Breeze Residence',
    address: 'Nardaran, Bakı',
    latitude: 40.594,
    longitude: 49.974,
    radius_m: 220,
    work_start: '08:00',
    work_end: '17:00',
  },
  {
    site_id: 'S-002',
    site_name: 'Yasamal City Towers',
    address: 'Yasamal, Bakı',
    latitude: 40.377,
    longitude: 49.812,
    radius_m: 180,
    work_start: '08:00',
    work_end: '17:00',
  },
  {
    site_id: 'S-003',
    site_name: 'Nizami Park Rezidens',
    address: 'Nizami, Bakı',
    latitude: 40.411,
    longitude: 49.943,
    radius_m: 160,
    work_start: '08:00',
    work_end: '17:00',
  },
  {
    site_id: 'S-004',
    site_name: 'Qobu Logistika Mərkəzi',
    address: 'Qobu, Abşeron',
    latitude: 40.404,
    longitude: 49.645,
    radius_m: 260,
    work_start: '08:00',
    work_end: '17:00',
  },
  {
    site_id: 'S-005',
    site_name: 'Sumqayıt Yaşayış Kompleksi',
    address: 'Sumqayıt',
    latitude: 40.59,
    longitude: 49.668,
    radius_m: 210,
    work_start: '08:00',
    work_end: '17:00',
  },
  {
    site_id: 'S-006',
    site_name: 'Gəncə Ticarət Mərkəzi',
    address: 'Gəncə',
    latitude: 40.682,
    longitude: 46.36,
    radius_m: 190,
    work_start: '08:00',
    work_end: '17:00',
  },
  {
    site_id: 'S-007',
    site_name: 'Lənkəran İstirahət Kompleksi',
    address: 'Lənkəran',
    latitude: 38.754,
    longitude: 48.851,
    radius_m: 200,
    work_start: '08:00',
    work_end: '17:00',
  },
  {
    site_id: 'S-008',
    site_name: 'Bakı Ofis Mərkəzi',
    address: 'Nərimanov, Bakı',
    latitude: 40.395,
    longitude: 49.863,
    radius_m: 140,
    work_start: '08:00',
    work_end: '17:00',
  },
]

const phaseTemplates = [
  ['CC-01-001', 'Beton işi', 2480, 450, 'm³'],
  ['CC-01-002', 'Armatur işi', 1860, 38, 'ton'],
  ['CC-01-003', 'Qəlib işi', 1520, 520, 'm²'],
  ['CC-01-004', 'Daş işi', 960, 320, 'm²'],
  ['CC-01-005', 'Elektrik işi', 1340, 1250, 'nöqtə'],
  ['CC-01-006', 'Santexnika işi', 1120, 850, 'nöqtə'],
  ['CC-01-007', 'Suvaq işi', 1980, 2400, 'm²'],
  ['CC-01-008', 'Alçıpan işi', 880, 980, 'm²'],
  ['CC-01-009', 'Boya işi', 760, 900, 'm²'],
  ['CC-01-010', 'Lift quraşdırılması', 1260, 4, 'ədəd'],
] as const

const seeded = (seed: number) => {
  let value = seed % 2147483647
  return () => {
    value = (value * 16807) % 2147483647
    return (value - 1) / 2147483646
  }
}

const pick = <T,>(items: T[], index: number) => items[index % items.length]

const asCompany = (company: Company[] | undefined): Company[] =>
  company?.length
    ? company
    : [
        {
          company_name: 'Ferstac Labs Construction',
          voen: '1700987651',
          contact_person: 'Aysel Əliyeva',
          phone: '+994 50 555 22 11',
        },
      ]

const asSites = (sites: Site[] | undefined): Site[] => (sites?.length ? sites : defaultSites)

const createWorkers = (workers?: Worker[]): Worker[] => {
  if (workers?.length) return workers

  return Array.from({ length: 112 }, (_, index) => {
    const salaryType: SalaryType = index % 9 === 0 ? 'Günlük' : index % 17 === 0 ? 'Aylıq' : 'Saatlıq'
    const baseRate = salaryType === 'Aylıq' ? 850 + (index % 9) * 80 : salaryType === 'Günlük' ? 45 + (index % 7) * 5 : 7.5 + (index % 12) * 0.65

    return {
      worker_id: `${1001 + index}`,
      full_name: `${pick(names, index)} ${index > names.length - 1 ? index + 1 : ''}`.trim(),
      phone: `+994 50 ${String(210 + index).padStart(3, '0')} ${String(30 + (index % 60)).padStart(2, '0')} ${String(10 + (index % 80)).padStart(2, '0')}`,
      position: pick(positions, index),
      brigade: pick(brigades, index),
      salary_type: salaryType,
      salary_rate: Number(baseRate.toFixed(2)),
      status: index % 31 === 0 ? 'Passiv' : 'Aktiv',
    }
  })
}

const createAssignments = (workers: Worker[], sites: Site[], assignments?: Assignment[]): Assignment[] => {
  if (assignments?.length) return assignments

  return workers.map((worker, index) => ({
    worker_id: worker.worker_id,
    site_id: pick(sites, index).site_id,
    start_date: '2024-12-01',
    end_date: '2025-12-31',
    active: worker.status === 'Aktiv',
  }))
}

const createWorkPhases = (sites: Site[], phases?: WorkPhase[]): WorkPhase[] => {
  if (phases?.length) return phases

  return phaseTemplates.map(([costCode, phaseName, plannedHours, plannedQuantity, unit], index) => ({
    phase_id: `P-${String(index + 1).padStart(3, '0')}`,
    site_id: pick(sites, index).site_id,
    cost_code: costCode,
    phase_name: phaseName,
    planned_hours: plannedHours,
    planned_quantity: plannedQuantity,
    unit,
  }))
}

const addMinutes = (time: string, minutes: number) => dayjs(`2025-05-01 ${time}`).add(minutes, 'minute').format('HH:mm')

const createAttendance = (workers: Worker[], sites: Site[], assignments: Assignment[]) => {
  const random = seeded(202505)
  const dates = getDatesInRange(DEFAULT_START_DATE, DEFAULT_END_DATE)
  const siteById = new Map(sites.map((site) => [site.site_id, site]))
  const assignmentByWorker = new Map(assignments.map((assignment) => [assignment.worker_id, assignment]))
  const records: AttendanceRecord[] = []

  workers
    .filter((worker) => worker.status === 'Aktiv')
    .forEach((worker, workerIndex) => {
      const assignment = assignmentByWorker.get(worker.worker_id)
      const site = assignment ? siteById.get(assignment.site_id) : pick(sites, workerIndex)
      if (!site) return

      dates.forEach((date, dateIndex) => {
        const weekday = dayjs(date).day()
        if (weekday === 0) return

        const behavior = (workerIndex * 13 + dateIndex * 7 + Math.floor(random() * 100)) % 100
        const plannedIn = site.work_start || '08:00'
        const plannedOut = site.work_end || '17:00'
        const isAbsent = behavior < 7
        const isLate = behavior >= 7 && behavior < 24
        const earlyLeave = behavior >= 24 && behavior < 31
        const missingCheckout = behavior >= 31 && behavior < 35
        const offline = behavior >= 35 && behavior < 43
        const tabletRepeat = behavior >= 43 && behavior < 51
        const manualCorrection = behavior >= 51 && behavior < 57
        const entryMethod: EntryMethod = offline
          ? 'Offline'
          : tabletRepeat
            ? 'Prorab Tablet'
            : manualCorrection
              ? 'Manual'
              : behavior % 3 === 0
                ? 'Mobil App'
                : 'Turniket'
        const lateMinutes = isLate ? 8 + ((workerIndex + dateIndex) % 42) : manualCorrection ? 5 : 0
        const earlyMinutes = earlyLeave ? 20 + ((workerIndex + dateIndex) % 55) : 0
        const overtime = !isAbsent && !missingCheckout && behavior > 78 ? 0.5 + ((workerIndex + dateIndex) % 5) * 0.5 : 0
        const worked = isAbsent
          ? 0
          : Math.max(0, 9 - lateMinutes / 60 - earlyMinutes / 60 - (missingCheckout ? 3.5 : 0) + overtime)
        let riskScore = 0
        if (tabletRepeat) riskScore += 15 + ((workerIndex + dateIndex) % 16)
        if (missingCheckout) riskScore += 25
        if (isLate && (workerIndex + dateIndex) % 3 === 0) riskScore += 10 + ((workerIndex + dateIndex) % 11)
        if (earlyLeave && (workerIndex + dateIndex) % 2 === 0) riskScore += 10 + ((workerIndex + dateIndex) % 6)
        if (offline) riskScore += 10 + ((workerIndex + dateIndex) % 11)
        if (manualCorrection) riskScore += 10 + ((workerIndex + dateIndex) % 16)
        if (behavior > 92) riskScore += 10 + ((workerIndex + dateIndex) % 11)
        riskScore = Math.min(100, riskScore)

        const status = isAbsent ? 'Gəlməyib' : isLate ? 'Gecikib' : earlyLeave ? 'Erkən çıxıb' : 'Gəlib'
        records.push({
          attendance_id: `A-${worker.worker_id}-${date}`,
          worker_id: worker.worker_id,
          site_id: site.site_id,
          date,
          planned_check_in: plannedIn,
          actual_check_in: isAbsent ? '' : addMinutes(plannedIn, lateMinutes - (behavior % 6)),
          planned_check_out: plannedOut,
          actual_check_out: isAbsent || missingCheckout ? '' : addMinutes(plannedOut, overtime * 60 - earlyMinutes + (behavior % 8)),
          status,
          late_minutes: lateMinutes,
          early_leave_minutes: earlyMinutes,
          worked_hours: Number(worked.toFixed(2)),
          overtime_hours: Number(overtime.toFixed(2)),
          entry_method: entryMethod,
          is_offline: offline,
          sync_time: offline ? `${date} ${addMinutes('17:10', 60 + (behavior % 120))}` : `${date} 17:10`,
          risk_score: riskScore,
          risk_level: calculateRiskLevel(riskScore),
          approved_by: pick(supervisors, workerIndex + dateIndex),
        })
      })
    })

  return records
}

const riskReason = (record: AttendanceRecord) => {
  const reasons: string[] = []
  if (record.entry_method === 'Prorab Tablet') reasons.push('Tablet giriş təkrarı')
  if (!record.actual_check_out && record.status !== 'Gəlməyib') reasons.push('Çıxış yoxdur')
  if (record.late_minutes > 0) reasons.push('Davamlı gecikmə')
  if (record.early_leave_minutes > 0) reasons.push('Erkən çıxış')
  if (record.is_offline) reasons.push('Offline sync gecikməsi')
  if (record.entry_method === 'Manual') reasons.push('Manual düzəliş')
  return reasons.join(', ') || 'Yüngül risk nümunəsi'
}

const createRiskRecords = (attendanceRecords: AttendanceRecord[]): RiskRecord[] =>
  attendanceRecords
    .filter((record) => record.risk_score >= 38)
    .slice(0, 180)
    .map((record, index) => ({
      risk_id: `R-${record.attendance_id}`,
      worker_id: record.worker_id,
      site_id: record.site_id,
      date: `${record.date} ${record.actual_check_in || '09:00'}`,
      risk_type: record.entry_method === 'Prorab Tablet' ? 'Tablet təkrarı' : record.is_offline ? 'Offline risk' : 'Zaman uyğunsuzluğu',
      risk_reason: riskReason(record),
      risk_score: record.risk_score,
      risk_level: record.risk_level,
      repeat_count: 1 + (index % 18),
      entry_method: record.entry_method,
      recommendation: riskRecommendation(record.risk_score),
    }))

const createPayroll = (workers: Worker[], sites: Site[], assignments: Assignment[], attendanceRecords: AttendanceRecord[]) => {
  const assignmentByWorker = new Map(assignments.map((assignment) => [assignment.worker_id, assignment]))
  const recordsByWorker = new Map<string, AttendanceRecord[]>()
  attendanceRecords.forEach((record) => {
    const current = recordsByWorker.get(record.worker_id) ?? []
    current.push(record)
    recordsByWorker.set(record.worker_id, current)
  })

  return workers
    .filter((worker) => worker.status === 'Aktiv')
    .map((worker, index): PayrollRecord => {
      const siteId = assignmentByWorker.get(worker.worker_id)?.site_id ?? pick(sites, index).site_id
      const records = recordsByWorker.get(worker.worker_id) ?? []
      const normalHours = records.reduce((sum, record) => sum + Math.max(0, record.worked_hours - record.overtime_hours), 0)
      const overtimeHours = records.reduce((sum, record) => sum + record.overtime_hours, 0)
      const permissionHours = records.filter((record) => record.status === 'İcazəli').length * 8
      const riskyHours = records.filter((record) => record.risk_score >= 60).reduce((sum, record) => sum + record.worked_hours, 0)
      const approvedHours = Math.max(0, normalHours + overtimeHours - riskyHours * 0.12)
      const hourlyRate =
        worker.salary_type === 'Aylıq' ? worker.salary_rate / 176 : worker.salary_type === 'Günlük' ? worker.salary_rate / 8 : worker.salary_rate
      const gross = normalHours * hourlyRate + overtimeHours * hourlyRate * 1.5
      const adjustment = index % 9 === 0 ? -47 : index % 13 === 0 ? 100 : 0

      return {
        payroll_id: `PAY-${worker.worker_id}-${DEFAULT_MONTH}`,
        worker_id: worker.worker_id,
        site_id: siteId,
        month: DEFAULT_MONTH,
        salary_type: worker.salary_type,
        salary_rate: worker.salary_rate,
        normal_hours: Number(normalHours.toFixed(1)),
        overtime_hours: Number(overtimeHours.toFixed(1)),
        permission_hours: permissionHours,
        risky_hours: Number(riskyHours.toFixed(1)),
        approved_hours: Number(approvedHours.toFixed(1)),
        gross_amount: Number(gross.toFixed(2)),
        adjustment,
        net_amount: Number((gross + adjustment).toFixed(2)),
        export_status: index % 19 === 0 ? 'Xəta' : index % 11 === 0 ? 'Xəbərdarlıq' : index % 7 === 0 ? 'Göndərilib' : 'Hazır',
      }
    })
}

const createSupervisorAudits = (sites: Site[]): SupervisorAuditRecord[] =>
  supervisors.slice(0, Math.min(supervisors.length, sites.length)).map((supervisor, index) => {
    const manual = 48 + index * 18 + (index % 3) * 14
    const risky = 3 + (index % 5) * 4
    const auditStatus = risky > 16 ? 'Riskli' : manual > 130 ? 'Nəzarət tələb edir' : index % 4 === 0 ? 'Yaxşı' : 'Uyğun'

    return {
      supervisor_id: `SUP-${index + 1}`,
      supervisor_name: supervisor.replace('.', 'ad'),
      site_id: pick(sites, index).site_id,
      period: '01.05 - 31.05.2025',
      tablet_entries: 48 + index * 11,
      manual_edits: manual,
      checkin_changes: 16 + index * 7,
      checkout_changes: 12 + index * 6,
      risky_approvals: risky,
      repeated_worker_confirmations: 1 + index * 2,
      late_approvals: 3 + index * 2,
      audit_status: auditStatus,
    }
  })

const createCostCodes = (workPhases: WorkPhase[], workers: Worker[]): CostCodeRecord[] =>
  workPhases.map((phase, index) => {
    const factor = 0.82 + (index % 7) * 0.035
    const actualHours = Number((phase.planned_hours * factor).toFixed(1))
    const productivity = Number(((actualHours / phase.planned_hours) * 100).toFixed(1))
    const actualQuantity = Number((phase.planned_quantity * (0.86 + (index % 5) * 0.03)).toFixed(1))
    const averageRate = workers.length
      ? workers.reduce((sum, worker) => sum + (worker.salary_type === 'Saatlıq' ? worker.salary_rate : worker.salary_type === 'Günlük' ? worker.salary_rate / 8 : worker.salary_rate / 176), 0) /
        workers.length
      : 9.5

    return {
      site_id: phase.site_id,
      cost_code: phase.cost_code,
      phase_name: phase.phase_name,
      brigade: pick(brigades, index),
      planned_hours: phase.planned_hours,
      actual_hours: actualHours,
      hour_difference: Number((actualHours - phase.planned_hours).toFixed(1)),
      planned_quantity: phase.planned_quantity,
      actual_quantity: actualQuantity,
      unit: phase.unit,
      productivity_percent: productivity,
      labor_cost: Number((actualHours * averageRate * 3.7).toFixed(0)),
      status: productivity < 88 ? 'Təkmilləşdirilməlidir' : productivity < 95 ? 'Orta' : 'Yaxşı',
    }
  })

export const generateBuildTrackData = (imported?: ImportedBaseData): BuildTrackData => {
  const company = asCompany(imported?.company)
  const sites = asSites(imported?.sites)
  const workers = createWorkers(imported?.workers)
  const assignments = createAssignments(workers, sites, imported?.assignments)
  const workPhases = createWorkPhases(sites, imported?.workPhases)
  const attendanceRecords = createAttendance(workers, sites, assignments)
  const riskRecords = createRiskRecords(attendanceRecords)
  const payrollRecords = createPayroll(workers, sites, assignments, attendanceRecords)
  const supervisorAuditRecords = createSupervisorAudits(sites)
  const costCodeRecords = createCostCodes(workPhases, workers)

  return {
    company,
    sites,
    workers,
    assignments,
    workPhases,
    attendanceRecords,
    riskRecords,
    payrollRecords,
    supervisorAuditRecords,
    costCodeRecords,
    generatedAt: new Date().toISOString(),
    source: imported ? 'imported' : 'sample',
  }
}
