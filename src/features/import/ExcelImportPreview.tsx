import { Alert, Tabs } from 'antd'
import type { TableColumnsType } from 'antd'
import { DataTable } from '../../components/tables/DataTable'
import type { ExcelImportResult } from '../../services/data/excelImportService'

interface PreviewRow extends Record<string, unknown> {
  key: string
}

export const ExcelImportPreview = ({ result }: { result: ExcelImportResult }) => (
  <section className="page-stack">
    {result.warnings.length ? (
      <Alert
        type={result.valid ? 'warning' : 'error'}
        showIcon
        message="Excel yoxlama nəticələri"
        description={<ul>{result.warnings.map((warning) => <li key={warning}>{warning}</li>)}</ul>}
      />
    ) : (
      <Alert type="success" showIcon message="Excel strukturu uyğundur" description="Bütün tələb olunan sheet və sütunlar tapıldı." />
    )}

    <Tabs
      items={Object.entries(result.previews).map(([sheetName, rows]) => {
        const previewRows: PreviewRow[] = rows.map((row, index) => ({ ...row, key: `${sheetName}-${index}` }))
        const columns: TableColumnsType<PreviewRow> = Object.keys(rows[0] ?? {}).map((key) => ({
          title: key,
          dataIndex: key,
          render: (value) => String(value ?? ''),
        }))

        return {
          key: sheetName,
          label: sheetName,
          children: <DataTable title={`${sheetName} preview`} columns={columns} data={previewRows} pageSize={10} />,
        }
      })}
    />
  </section>
)
