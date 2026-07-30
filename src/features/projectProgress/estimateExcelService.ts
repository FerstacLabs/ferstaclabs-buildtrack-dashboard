import * as XLSX from 'xlsx'
import type { AppLanguage } from '../../i18n'
import type { Crew, MaterialItem, ProjectEstimateSummary, ProjectWorkStatus, WorkItem, WorkStage } from '../../types/projectProgress'

export interface ParsedEstimateRow {
  rowNumber: number
  stageName: string
  workName: string
  costCode?: string
  unit: string
  quantity: number
  completedQuantity: number
  laborUnitPrice: number
  laborTotal: number
  materialName?: string
  materialUnit: string
  materialQuantity: number
  materialUnitPrice: number
  materialTotal: number
  totalCost: number
  plannedHours: number
  actualHours: number
  crewName?: string
  status: ProjectWorkStatus
  progressPercent: number
  plannedStartDate?: string
  plannedEndDate?: string
  notes?: string
}

export interface InvalidEstimateRow {
  rowNumber: number
  reason: string
}

export interface EstimateWorkbookParseResult {
  sheetNames: string[]
  previewRows: unknown[][]
  rows: ParsedEstimateRow[]
  invalidRows: InvalidEstimateRow[]
}

export interface EstimateImportSummary {
  importedRows: number
  createdStages: number
  createdCrews: number
  createdMaterials: number
  skippedRows: number
  invalidRows: InvalidEstimateRow[]
}

const estimateSheetHints = ['kaba işlər smetası', 'smeta', 'estimate', 'work items', 'смета', 'работы']

const headerAliases: Record<keyof Omit<ParsedEstimateRow, 'rowNumber' | 'status'> | 'status', string[]> = {
  stageName: ['etap', 'mərhələ', 'stage', 'work stage', 'раздел', 'этап', 'стадия'],
  workName: ['iş adı', 'iş', 'işin adı', 'work name', 'work', 'description', 'item', 'наименование работ', 'работа', 'название работы'],
  costCode: ['cost code', 'kod', 'iş kodu', 'код', 'код работы'],
  unit: ['iş vahidi', 'vahid', 'ölçü vahidi', 'unit', 'work unit', 'единица', 'ед. изм.', 'единица работы'],
  quantity: ['miqdar', 'həcm', 'quantity', 'qty', 'volume', 'количество', 'объем', 'объём'],
  completedQuantity: ['tamamlanan', 'tamamlanan miqdar', 'icra olunan', 'completed', 'completed quantity', 'выполнено', 'выполненный объем'],
  laborUnitPrice: ['işçilik vahid qiyməti', 'işçilik qiyməti', 'labor unit price', 'labor price', 'цена работы', 'цена работ'],
  laborTotal: ['işçilik', 'işçilik cəmi', 'labor total', 'labor', 'стоимость работ', 'работа сумма'],
  materialName: ['material', 'material adı', 'material name', 'материал', 'название материала'],
  materialUnit: ['material ölçü vahidi', 'material vahidi', 'material unit', 'единица материала', 'ед. материала'],
  materialQuantity: ['material miqdarı', 'material quantity', 'material qty', 'количество материала', 'объем материала'],
  materialUnitPrice: ['material vahid qiyməti', 'material unit price', 'material price', 'цена материала'],
  materialTotal: ['material cəmi', 'material total', 'materials total', 'стоимость материала', 'материал сумма'],
  totalCost: ['ümumi xərc', 'ümumi məbləğ', 'cəmi', 'yekun', 'total cost', 'total', 'sum', 'итого', 'общая стоимость'],
  plannedHours: ['plan saat', 'planned hours', 'plan hours', 'план часы', 'плановые часы'],
  actualHours: ['faktiki saat', 'actual hours', 'fact hours', 'факт часы', 'фактические часы'],
  crewName: ['briqada', 'crew', 'brigade', 'бригада'],
  status: ['status', 'статус'],
  progressPercent: ['gedişat %', 'gedişat', 'progress %', 'progress', 'процент выполнения', 'прогресс'],
  plannedStartDate: ['başlama tarixi', 'start date', 'planned start', 'дата начала'],
  plannedEndDate: ['bitmə tarixi', 'plan bitmə tarixi', 'end date', 'planned end', 'дата окончания'],
  notes: ['qeyd', 'notes', 'note', 'примечание', 'комментарий'],
}

const normalizeText = (value: unknown) => String(value ?? '')
  .trim()
  .toLocaleLowerCase('az-AZ')
  .replace(/[^\w%əöğüşçıİıƏÖĞÜŞÇа-яА-ЯёЁ]+/g, ' ')
  .replace(/\s+/g, ' ')
  .trim()

const aliasLookup = new Map<string, keyof typeof headerAliases>(
  Object.entries(headerAliases).flatMap(([key, aliases]) => aliases.map((alias) => [normalizeText(alias), key as keyof typeof headerAliases])),
)

const parseNumber = (value: unknown, fallback = 0) => {
  if (typeof value === 'number' && Number.isFinite(value)) return value
  const raw = String(value ?? '').trim()
  if (!raw) return fallback

  const cleaned = raw
    .replace(/azn|₼|руб\.?|rub|usd|\$/gi, '')
    .replace(/%/g, '')
    .replace(/\s+/g, '')

  const decimalNormalized = cleaned.includes(',') && !cleaned.includes('.')
    ? cleaned.replace(',', '.')
    : cleaned.replace(/,/g, '')
  const parsed = Number(decimalNormalized)
  return Number.isFinite(parsed) ? parsed : fallback
}

const parsePercent = (value: unknown, fallback = 0) => {
  const parsed = parseNumber(value, fallback)
  return Math.max(0, Math.min(100, parsed > 0 && parsed <= 1 ? parsed * 100 : parsed))
}

const parseDate = (value: unknown) => {
  if (value instanceof Date && !Number.isNaN(value.getTime())) return value.toISOString().slice(0, 10)
  if (typeof value === 'number' && Number.isFinite(value)) {
    const parsed = XLSX.SSF.parse_date_code(value)
    if (parsed) return `${parsed.y}-${String(parsed.m).padStart(2, '0')}-${String(parsed.d).padStart(2, '0')}`
  }

  const raw = String(value ?? '').trim()
  if (!raw) return undefined
  const iso = raw.match(/^(\d{4})[-/.](\d{1,2})[-/.](\d{1,2})/)
  if (iso) return `${iso[1]}-${iso[2].padStart(2, '0')}-${iso[3].padStart(2, '0')}`
  const local = raw.match(/^(\d{1,2})[-/.](\d{1,2})[-/.](\d{4})/)
  if (local) return `${local[3]}-${local[2].padStart(2, '0')}-${local[1].padStart(2, '0')}`
  return undefined
}

const parseStatus = (value: unknown): ProjectWorkStatus => {
  const normalized = normalizeText(value)
  if (!normalized) return 'NotStarted'
  if (['completed', 'done', 'tamamlanıb', 'tamamlanib', 'завершено', 'готово'].some((entry) => normalized.includes(entry))) return 'Completed'
  if (['delayed', 'gecikir', 'gecikmə', 'задерж', 'опоздан'].some((entry) => normalized.includes(entry))) return 'Delayed'
  if (['paused', 'dayandırılıb', 'dayandirilib', 'приостанов'].some((entry) => normalized.includes(entry))) return 'Paused'
  if (['in progress', 'icradadır', 'icradadir', 'icra', 'в работе', 'в процессе'].some((entry) => normalized.includes(entry))) return 'InProgress'
  return 'NotStarted'
}

const getMappedCell = (row: Record<string, unknown>, field: keyof typeof headerAliases) => {
  for (const [header, value] of Object.entries(row)) {
    if (aliasLookup.get(normalizeText(header)) === field) return value
  }
  return undefined
}

const chooseEstimateSheetName = (workbook: XLSX.WorkBook) => {
  const found = workbook.SheetNames.find((name) => estimateSheetHints.some((hint) => normalizeText(name).includes(normalizeText(hint))))
  return found ?? workbook.SheetNames[0]
}

export const parseEstimateWorkbook = async (file: File): Promise<EstimateWorkbookParseResult> => {
  const buffer = await file.arrayBuffer()
  const workbook = XLSX.read(buffer, { type: 'array', cellDates: true })
  const sheetName = chooseEstimateSheetName(workbook)
  const sheet = workbook.Sheets[sheetName]
  const previewRows = XLSX.utils.sheet_to_json<unknown[]>(sheet, { header: 1, blankrows: false, defval: '' }).slice(0, 25)
  const rawRows = XLSX.utils.sheet_to_json<Record<string, unknown>>(sheet, { defval: '', raw: false })

  const rows: ParsedEstimateRow[] = []
  const invalidRows: InvalidEstimateRow[] = []

  rawRows.forEach((row, index) => {
    const rowNumber = index + 2
    const stageName = String(getMappedCell(row, 'stageName') ?? '').trim()
    const workName = String(getMappedCell(row, 'workName') ?? '').trim()
    if (!stageName || !workName) {
      invalidRows.push({ rowNumber, reason: !stageName ? 'Etap boşdur' : 'İş adı boşdur' })
      return
    }

    const quantity = parseNumber(getMappedCell(row, 'quantity'), 1)
    const completedQuantity = parseNumber(getMappedCell(row, 'completedQuantity'), 0)
    const laborTotalInput = parseNumber(getMappedCell(row, 'laborTotal'), 0)
    const laborUnitPriceInput = parseNumber(getMappedCell(row, 'laborUnitPrice'), quantity > 0 ? laborTotalInput / quantity : 0)
    const materialQuantity = parseNumber(getMappedCell(row, 'materialQuantity'), 0)
    const materialTotalInput = parseNumber(getMappedCell(row, 'materialTotal'), 0)
    const materialUnitPriceInput = parseNumber(getMappedCell(row, 'materialUnitPrice'), materialQuantity > 0 ? materialTotalInput / materialQuantity : 0)
    const laborTotal = laborTotalInput || Math.round(quantity * laborUnitPriceInput * 100) / 100
    const materialTotal = materialTotalInput || Math.round(materialQuantity * materialUnitPriceInput * 100) / 100
    const totalCost = parseNumber(getMappedCell(row, 'totalCost'), laborTotal + materialTotal)
    const status = parseStatus(getMappedCell(row, 'status'))
    const progressFromQuantity = quantity > 0 ? Math.round((completedQuantity / quantity) * 1000) / 10 : 0
    const progressPercent = parsePercent(getMappedCell(row, 'progressPercent'), completedQuantity > 0 ? progressFromQuantity : status === 'Completed' ? 100 : 0)

    rows.push({
      rowNumber,
      stageName,
      workName,
      costCode: String(getMappedCell(row, 'costCode') ?? '').trim() || undefined,
      unit: String(getMappedCell(row, 'unit') ?? '').trim() || 'iş',
      quantity,
      completedQuantity,
      laborUnitPrice: laborUnitPriceInput,
      laborTotal,
      materialName: String(getMappedCell(row, 'materialName') ?? '').trim() || undefined,
      materialUnit: String(getMappedCell(row, 'materialUnit') ?? '').trim() || 'ədəd',
      materialQuantity,
      materialUnitPrice: materialUnitPriceInput,
      materialTotal,
      totalCost,
      plannedHours: parseNumber(getMappedCell(row, 'plannedHours'), 0),
      actualHours: parseNumber(getMappedCell(row, 'actualHours'), 0),
      crewName: String(getMappedCell(row, 'crewName') ?? '').trim() || undefined,
      status,
      progressPercent,
      plannedStartDate: parseDate(getMappedCell(row, 'plannedStartDate')),
      plannedEndDate: parseDate(getMappedCell(row, 'plannedEndDate')),
      notes: String(getMappedCell(row, 'notes') ?? '').trim() || undefined,
    })
  })

  return { sheetNames: workbook.SheetNames, previewRows, rows, invalidRows }
}

const sheetNames: Record<AppLanguage, { summary: string; estimate: string; materials: string }> = {
  az: { summary: 'Xülasə', estimate: 'Smeta', materials: 'Materiallar' },
  en: { summary: 'Summary', estimate: 'Estimate', materials: 'Materials' },
  ru: { summary: 'Итоги', estimate: 'Смета', materials: 'Материалы' },
}

const exportHeaders: Record<AppLanguage, Record<string, string>> = {
  az: {
    stage: 'Etap',
    workName: 'İş adı',
    costCode: 'Cost Code',
    workUnit: 'İş vahidi',
    quantity: 'Miqdar',
    completedQuantity: 'Tamamlanan miqdar',
    laborUnitPrice: 'İşçilik vahid qiyməti',
    laborTotal: 'İşçilik cəmi',
    materialName: 'Material adı',
    materialUnit: 'Material ölçü vahidi',
    materialQuantity: 'Material miqdarı',
    materialUnitPrice: 'Material vahid qiyməti',
    materialTotal: 'Material cəmi',
    totalCost: 'Ümumi xərc',
    plannedHours: 'Plan saat',
    actualHours: 'Faktiki saat',
    crew: 'Briqada',
    status: 'Status',
    progress: 'Gedişat %',
    notes: 'Qeyd',
  },
  en: {
    stage: 'Stage',
    workName: 'Work name',
    costCode: 'Cost Code',
    workUnit: 'Work unit',
    quantity: 'Quantity',
    completedQuantity: 'Completed quantity',
    laborUnitPrice: 'Labor unit price',
    laborTotal: 'Labor total',
    materialName: 'Material name',
    materialUnit: 'Material measurement unit',
    materialQuantity: 'Material quantity',
    materialUnitPrice: 'Material unit price',
    materialTotal: 'Material total',
    totalCost: 'Total cost',
    plannedHours: 'Planned hours',
    actualHours: 'Actual hours',
    crew: 'Crew',
    status: 'Status',
    progress: 'Progress %',
    notes: 'Notes',
  },
  ru: {
    stage: 'Этап',
    workName: 'Название работы',
    costCode: 'Cost Code',
    workUnit: 'Единица работы',
    quantity: 'Количество',
    completedQuantity: 'Выполненный объем',
    laborUnitPrice: 'Цена работы',
    laborTotal: 'Стоимость работ',
    materialName: 'Название материала',
    materialUnit: 'Единица материала',
    materialQuantity: 'Количество материала',
    materialUnitPrice: 'Цена материала',
    materialTotal: 'Стоимость материала',
    totalCost: 'Итого',
    plannedHours: 'План часы',
    actualHours: 'Факт часы',
    crew: 'Бригада',
    status: 'Статус',
    progress: 'Прогресс %',
    notes: 'Примечание',
  },
}

const fitColumns = (worksheet: XLSX.WorkSheet, rows: Record<string, unknown>[]) => {
  const headers = Object.keys(rows[0] ?? {})
  worksheet['!cols'] = headers.map((header) => ({
    wch: Math.min(42, Math.max(header.length + 2, ...rows.map((row) => String(row[header] ?? '').length + 2))),
  }))
  ;(worksheet as XLSX.WorkSheet & { '!freeze'?: unknown })['!freeze'] = { xSplit: 0, ySplit: 1 }
}

const appendJsonSheet = (workbook: XLSX.WorkBook, rows: Record<string, unknown>[], name: string) => {
  const worksheet = XLSX.utils.json_to_sheet(rows)
  fitColumns(worksheet, rows)
  XLSX.utils.book_append_sheet(workbook, worksheet, name)
}

export const getLocalizedEstimateHeaders = (language: AppLanguage) => exportHeaders[language]

export const downloadEstimateTemplate = (language: AppLanguage) => {
  const h = exportHeaders[language]
  const workbook = XLSX.utils.book_new()
  appendJsonSheet(workbook, [
    {
      [h.stage]: 'Torpaq işləri',
      [h.workName]: 'Torpaq qazıntısı',
      [h.costCode]: 'TRP-001',
      [h.workUnit]: 'm³',
      [h.quantity]: 120,
      [h.completedQuantity]: 0,
      [h.laborUnitPrice]: 8,
      [h.materialName]: '',
      [h.materialUnit]: '',
      [h.materialQuantity]: 0,
      [h.materialUnitPrice]: 0,
      [h.plannedHours]: 48,
      [h.actualHours]: 0,
      [h.crew]: 'Monolit briqadası',
      [h.status]: 'Başlamayıb',
      [h.progress]: 0,
      [h.notes]: 'Demo sətir',
    },
    {
      [h.stage]: 'Monolit dəmir beton',
      [h.workName]: 'Beton B25 tökülməsi',
      [h.costCode]: 'MON-002',
      [h.workUnit]: 'm³',
      [h.quantity]: 35,
      [h.completedQuantity]: 12,
      [h.laborUnitPrice]: 18,
      [h.materialName]: 'Beton B25',
      [h.materialUnit]: 'm³',
      [h.materialQuantity]: 35,
      [h.materialUnitPrice]: 145,
      [h.plannedHours]: 72,
      [h.actualHours]: 24,
      [h.crew]: 'Monolit briqadası',
      [h.status]: 'İcradadır',
      [h.progress]: 34,
      [h.notes]: 'Material və iş vahidləri ayrıdır',
    },
  ], sheetNames[language].estimate)
  XLSX.writeFile(workbook, `BuildTrack_Smeta_Template_${language}.xlsx`)
}

export const exportEstimateWorkbook = ({
  crews,
  estimateVersionName,
  language,
  materials,
  objectName,
  projectName,
  stageNameById,
  statusText,
  summary,
  workItems,
}: {
  projectName: string
  objectName: string
  estimateVersionName: string
  summary: ProjectEstimateSummary
  stages: WorkStage[]
  workItems: WorkItem[]
  crews: Crew[]
  materials: MaterialItem[]
  language: AppLanguage
  stageNameById: Map<string, string>
  statusText: (status: ProjectWorkStatus) => string
}) => {
  const workbook = XLSX.utils.book_new()
  const h = exportHeaders[language]
  const crewNameById = new Map(crews.map((crew) => [crew.id, crew.name]))

  appendJsonSheet(workbook, [
    {
      Layihə: projectName,
      Obyekt: objectName,
      Versiya: estimateVersionName,
      Valyuta: summary.currency,
      'Yekun smeta': summary.totalAmount,
      İşçilik: summary.laborAmount,
      Material: summary.materialAmount,
      'Gözə görünməyən xərclər': summary.hiddenCostAmount,
    },
  ], sheetNames[language].summary)

  appendJsonSheet(workbook, workItems.map((item) => {
    const material = materials.find((entry) => entry.linkedWorkItemId === item.id)
    return {
      [h.stage]: stageNameById.get(item.stageId) ?? item.stageId,
      [h.workName]: item.name,
      [h.costCode]: item.costCode ?? '',
      [h.workUnit]: item.unit,
      [h.quantity]: item.quantity,
      [h.completedQuantity]: item.completedQuantity ?? 0,
      [h.laborUnitPrice]: item.laborUnitPrice,
      [h.laborTotal]: item.laborTotal,
      [h.materialName]: material?.name ?? '',
      [h.materialUnit]: material?.unit ?? item.materialUnit ?? '',
      [h.materialQuantity]: material?.quantity ?? item.materialQuantity,
      [h.materialUnitPrice]: material?.unitPrice ?? item.materialUnitPrice,
      [h.materialTotal]: item.materialTotal,
      [h.totalCost]: item.totalCost,
      [h.plannedHours]: item.plannedHours,
      [h.actualHours]: item.actualHours,
      [h.crew]: item.assignedCrewId ? crewNameById.get(item.assignedCrewId) ?? '' : '',
      [h.status]: statusText(item.status),
      [h.progress]: item.progressPercent,
      [h.notes]: item.notes ?? '',
    }
  }), sheetNames[language].estimate)

  appendJsonSheet(workbook, materials.map((material) => ({
    Material: material.name,
    Vahid: material.unit,
    Miqdar: material.quantity,
    'İstifadə olunub': material.usedQuantity,
    Qalıq: material.remainingQuantity,
    'Vahid qiymət': material.unitPrice ?? 0,
    Təchizatçı: material.supplier ?? '',
    Etap: material.linkedStageId ? stageNameById.get(material.linkedStageId) ?? '' : '',
  })), sheetNames[language].materials)

  const date = new Intl.DateTimeFormat('en-CA').format(new Date())
  const safeProjectName = `${projectName}-${objectName}`.replace(/[^\p{L}\p{N}]+/gu, '-').replace(/^-|-$/g, '')
  XLSX.writeFile(workbook, `BuildTrack_Smeta_${safeProjectName}_${date}.xlsx`)
}
