import * as XLSX from 'xlsx'
import type { ExportValidationRow, PayrollRow } from '../../types/reports'
import { downloadBlob } from '../../utils/formatters'

type ExportableValue = string | number | boolean | null | undefined
type ExportableRow = object

const safeRows = <T extends ExportableRow>(rows: T[]) =>
  rows.map((row) =>
    Object.fromEntries(
      Object.entries(row as Record<string, ExportableValue>).filter(([key]) => key !== 'key'),
    ) as Record<string, ExportableValue>,
  )

export const exportRowsToExcel = <T extends ExportableRow>(fileName: string, rows: T[]) => {
  const worksheet = XLSX.utils.json_to_sheet(safeRows(rows))
  const workbook = XLSX.utils.book_new()
  XLSX.utils.book_append_sheet(workbook, worksheet, 'BuildTrack')
  XLSX.writeFile(workbook, `${fileName}.xlsx`)
}

export const exportRowsToCsv = <T extends ExportableRow>(fileName: string, rows: T[]) => {
  const worksheet = XLSX.utils.json_to_sheet(safeRows(rows))
  const csv = XLSX.utils.sheet_to_csv(worksheet)
  downloadBlob(`${fileName}.csv`, new Blob([csv], { type: 'text/csv;charset=utf-8' }))
}

export const exportPayrollTo1CXml = (fileName: string, rows: PayrollRow[]) => {
  const body = rows
    .map(
      (row) => `
    <PayrollLine>
      <Worker>${row.full_name}</Worker>
      <Site>${row.site_name}</Site>
      <SalaryType>${row.salary_type}</SalaryType>
      <ApprovedHours>${row.approved_hours}</ApprovedHours>
      <NetAmount>${row.net_amount}</NetAmount>
      <Status>${row.export_status}</Status>
    </PayrollLine>`,
    )
    .join('')
  const xml = `<?xml version="1.0" encoding="UTF-8"?>
<BuildTrackPayroll exportDate="${new Date().toISOString()}">${body}
</BuildTrackPayroll>`

  downloadBlob(`${fileName}.xml`, new Blob([xml], { type: 'application/xml;charset=utf-8' }))
}

export const exportValidationToTxt = (fileName: string, rows: ExportValidationRow[]) => {
  const text = rows
    .map(
      (row) =>
        `${row.row_id};${row.full_name};${row.site_name};${row.cost_code};${row.salary_type};${row.approved_hours};${row.net_amount};${row.account_code};${row.export_status};${row.error_message}`,
    )
    .join('\n')

  downloadBlob(`${fileName}.txt`, new Blob([text], { type: 'text/plain;charset=utf-8' }))
}

const escapeXml = (value: ExportableValue) => String(value ?? '').replace(/[<>&'"]/g, (char) => {
  if (char === '<') return '&lt;'
  if (char === '>') return '&gt;'
  if (char === '&') return '&amp;'
  if (char === "'") return '&apos;'
  return '&quot;'
})

export const exportRowsTo1CMock = <T extends ExportableRow>(fileName: string, rows: T[]) => {
  const lines = safeRows(rows)
    .map((row) => {
      const fields = Object.entries(row)
        .map(([key, value]) => `      <Field name="${escapeXml(key)}">${escapeXml(value)}</Field>`)
        .join('\n')
      return `    <ReportLine>\n${fields}\n    </ReportLine>`
    })
    .join('\n')
  const xml = `<?xml version="1.0" encoding="UTF-8"?>\n<BuildTrackCustomReport exportDate="${new Date().toISOString()}">\n${lines}\n</BuildTrackCustomReport>`

  downloadBlob(`${fileName}.xml`, new Blob([xml], { type: 'application/xml;charset=utf-8' }))
}
