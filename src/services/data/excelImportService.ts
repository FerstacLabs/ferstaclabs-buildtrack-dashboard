import * as XLSX from 'xlsx'
import type { Assignment, Company, ImportedBaseData, SalaryType, Site, Worker, WorkPhase } from '../../types/models'

export interface ExcelImportResult {
  valid: boolean
  warnings: string[]
  data: ImportedBaseData
  previews: Record<string, Record<string, unknown>[]>
}

const requiredSheets = ['Company', 'Sites', 'Workers', 'Assignments', 'WorkPhases'] as const

const requiredColumns: Record<(typeof requiredSheets)[number], string[]> = {
  Company: ['company_name', 'voen', 'contact_person', 'phone'],
  Sites: ['site_id', 'site_name', 'address', 'latitude', 'longitude', 'radius_m', 'work_start', 'work_end'],
  Workers: ['worker_id', 'full_name', 'phone', 'position', 'brigade', 'salary_type', 'salary_rate', 'status'],
  Assignments: ['worker_id', 'site_id', 'start_date', 'end_date', 'active'],
  WorkPhases: ['phase_id', 'site_id', 'cost_code', 'phase_name', 'planned_hours', 'planned_quantity', 'unit'],
}

const asString = (value: unknown) => String(value ?? '').trim()
const asNumber = (value: unknown) => Number(value || 0)
const asBool = (value: unknown) => {
  const normalized = asString(value).toLowerCase()
  return normalized === 'true' || normalized === '1' || normalized === 'bəli' || normalized === 'yes' || normalized === 'aktiv'
}
const asSalaryType = (value: unknown): SalaryType => {
  const normalized = asString(value)
  if (normalized === 'Günlük' || normalized === 'Aylıq') return normalized
  return 'Saatlıq'
}

const sheetRows = (workbook: XLSX.WorkBook, sheetName: string) => {
  const sheet = workbook.Sheets[sheetName]
  return sheet ? XLSX.utils.sheet_to_json<Record<string, unknown>>(sheet, { defval: '' }) : []
}

export const parseExcelFile = async (file: File): Promise<ExcelImportResult> => {
  const workbook = XLSX.read(await file.arrayBuffer(), { type: 'array' })
  const warnings: string[] = []
  const previews: Record<string, Record<string, unknown>[]> = {}

  requiredSheets.forEach((sheetName) => {
    if (!workbook.SheetNames.includes(sheetName)) {
      warnings.push(`"${sheetName}" sheet-i tapılmadı.`)
      previews[sheetName] = []
      return
    }

    const rows = sheetRows(workbook, sheetName)
    previews[sheetName] = rows.slice(0, 20)
    const availableColumns = rows[0] ? Object.keys(rows[0]) : []
    requiredColumns[sheetName].forEach((column) => {
      if (!availableColumns.includes(column)) {
        warnings.push(`"${sheetName}" sheet-ində "${column}" sütunu yoxdur.`)
      }
    })
  })

  const company = sheetRows(workbook, 'Company').map(
    (row): Company => ({
      company_name: asString(row.company_name),
      voen: asString(row.voen),
      contact_person: asString(row.contact_person),
      phone: asString(row.phone),
    }),
  )

  const sites = sheetRows(workbook, 'Sites').map(
    (row): Site => ({
      site_id: asString(row.site_id),
      site_name: asString(row.site_name),
      address: asString(row.address),
      latitude: asNumber(row.latitude),
      longitude: asNumber(row.longitude),
      radius_m: asNumber(row.radius_m),
      work_start: asString(row.work_start) || '08:00',
      work_end: asString(row.work_end) || '17:00',
    }),
  )

  const workers = sheetRows(workbook, 'Workers').map(
    (row): Worker => ({
      worker_id: asString(row.worker_id),
      full_name: asString(row.full_name),
      phone: asString(row.phone),
      position: asString(row.position),
      brigade: asString(row.brigade),
      salary_type: asSalaryType(row.salary_type),
      salary_rate: asNumber(row.salary_rate),
      status: asString(row.status) === 'Passiv' ? 'Passiv' : 'Aktiv',
    }),
  )

  const assignments = sheetRows(workbook, 'Assignments').map(
    (row): Assignment => ({
      worker_id: asString(row.worker_id),
      site_id: asString(row.site_id),
      start_date: asString(row.start_date),
      end_date: asString(row.end_date),
      active: asBool(row.active),
    }),
  )

  const workPhases = sheetRows(workbook, 'WorkPhases').map(
    (row): WorkPhase => ({
      phase_id: asString(row.phase_id),
      site_id: asString(row.site_id),
      cost_code: asString(row.cost_code),
      phase_name: asString(row.phase_name),
      planned_hours: asNumber(row.planned_hours),
      planned_quantity: asNumber(row.planned_quantity),
      unit: asString(row.unit),
    }),
  )

  if (company.length === 0) warnings.push('Company sheet-ində ən azı 1 sətir olmalıdır.')
  if (sites.length === 0) warnings.push('Sites sheet-ində ən azı 1 layihə olmalıdır.')
  if (workers.length === 0) warnings.push('Workers sheet-ində ən azı 1 işçi olmalıdır.')

  return {
    valid: warnings.length === 0,
    warnings,
    previews,
    data: {
      company,
      sites,
      workers,
      assignments,
      workPhases,
    },
  }
}
