import Dexie, { type Table } from 'dexie'
import type {
  Assignment,
  AttendanceRecord,
  BuildTrackData,
  Company,
  CostCodeRecord,
  PayrollRecord,
  RiskRecord,
  Site,
  SupervisorAuditRecord,
  Worker,
  WorkPhase,
} from '../../types/models'

interface MetaRecord {
  key: string
  value: string
}

class BuildTrackDatabase extends Dexie {
  meta!: Table<MetaRecord, string>
  companies!: Table<Company, string>
  sites!: Table<Site, string>
  workers!: Table<Worker, string>
  assignments!: Table<Assignment, number>
  workPhases!: Table<WorkPhase, string>
  attendanceRecords!: Table<AttendanceRecord, string>
  riskRecords!: Table<RiskRecord, string>
  payrollRecords!: Table<PayrollRecord, string>
  supervisorAuditRecords!: Table<SupervisorAuditRecord, string>
  costCodeRecords!: Table<CostCodeRecord, number>

  constructor() {
    super('buildtrack-demo-db')
    this.version(1).stores({
      meta: 'key',
      companies: 'company_name',
      sites: 'site_id',
      workers: 'worker_id',
      assignments: '++id, worker_id, site_id, active',
      workPhases: 'phase_id, site_id, cost_code',
      attendanceRecords: 'attendance_id, worker_id, site_id, date',
      riskRecords: 'risk_id, worker_id, site_id, date, risk_level',
      payrollRecords: 'payroll_id, worker_id, site_id, month',
      supervisorAuditRecords: 'supervisor_id, site_id, audit_status',
      costCodeRecords: '++id, site_id, cost_code',
    })
  }
}

export const db = new BuildTrackDatabase()

export const saveDataset = async (data: BuildTrackData) => {
  await db.transaction(
    'rw',
    [
      db.meta,
      db.companies,
      db.sites,
      db.workers,
      db.assignments,
      db.workPhases,
      db.attendanceRecords,
      db.riskRecords,
      db.payrollRecords,
      db.supervisorAuditRecords,
      db.costCodeRecords,
    ],
    async () => {
      await Promise.all([
        db.meta.clear(),
        db.companies.clear(),
        db.sites.clear(),
        db.workers.clear(),
        db.assignments.clear(),
        db.workPhases.clear(),
        db.attendanceRecords.clear(),
        db.riskRecords.clear(),
        db.payrollRecords.clear(),
        db.supervisorAuditRecords.clear(),
        db.costCodeRecords.clear(),
      ])

      await db.meta.bulkPut([
        { key: 'generatedAt', value: data.generatedAt },
        { key: 'source', value: data.source },
      ])
      await db.companies.bulkPut(data.company)
      await db.sites.bulkPut(data.sites)
      await db.workers.bulkPut(data.workers)
      await db.assignments.bulkAdd(data.assignments)
      await db.workPhases.bulkPut(data.workPhases)
      await db.attendanceRecords.bulkPut(data.attendanceRecords)
      await db.riskRecords.bulkPut(data.riskRecords)
      await db.payrollRecords.bulkPut(data.payrollRecords)
      await db.supervisorAuditRecords.bulkPut(data.supervisorAuditRecords)
      await db.costCodeRecords.bulkAdd(data.costCodeRecords)
    },
  )
}

export const loadDataset = async (): Promise<BuildTrackData | null> => {
  const [source, generatedAt, company, sites, workers] = await Promise.all([
    db.meta.get('source'),
    db.meta.get('generatedAt'),
    db.companies.toArray(),
    db.sites.toArray(),
    db.workers.toArray(),
  ])

  if (!source || sites.length === 0 || workers.length === 0) {
    return null
  }

  const [
    assignments,
    workPhases,
    attendanceRecords,
    riskRecords,
    payrollRecords,
    supervisorAuditRecords,
    costCodeRecords,
  ] = await Promise.all([
    db.assignments.toArray(),
    db.workPhases.toArray(),
    db.attendanceRecords.toArray(),
    db.riskRecords.toArray(),
    db.payrollRecords.toArray(),
    db.supervisorAuditRecords.toArray(),
    db.costCodeRecords.toArray(),
  ])

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
    generatedAt: generatedAt?.value ?? new Date().toISOString(),
    source: source.value === 'imported' ? 'imported' : 'sample',
  }
}

export const clearDataset = async () => {
  await db.delete()
  await db.open()
}
