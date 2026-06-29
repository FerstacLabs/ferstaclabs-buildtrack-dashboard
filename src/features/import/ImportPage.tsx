import { InboxOutlined, ReloadOutlined, UploadOutlined } from '@ant-design/icons'
import { Alert, Button, Upload, message } from 'antd'
import { useState } from 'react'
import { PageTitle } from '../../components/ui/PageTitle'
import { ToolbarButton } from '../../components/ui/ToolbarButton'
import { parseExcelFile, type ExcelImportResult } from '../../services/data/excelImportService'
import { useBuildTrackStore } from '../../services/data/dataService'
import { ExcelImportPreview } from './ExcelImportPreview'

export const ImportPage = () => {
  const { data, generateSampleData, resetDemoData, saveImportedData } = useBuildTrackStore()
  const [result, setResult] = useState<ExcelImportResult | null>(null)
  const [parsing, setParsing] = useState(false)

  return (
    <div className="page-stack">
      <PageTitle title="Excel Import" subtitle="Company, Sites, Workers, Assignments və WorkPhases sheet-lərini yükləyin" />

      <section className="import-grid">
        <section className="panel-card">
          <Upload.Dragger
            accept=".xlsx,.xls"
            maxCount={1}
            showUploadList={false}
            beforeUpload={(file) => {
              setParsing(true)
              parseExcelFile(file)
                .then((parsed) => {
                  setResult(parsed)
                  void message[parsed.valid ? 'success' : 'error'](parsed.valid ? 'Excel oxundu' : 'Excel strukturunda xəta var')
                })
                .catch((error: unknown) => {
                  void message.error(error instanceof Error ? error.message : 'Excel faylı oxunmadı')
                })
                .finally(() => setParsing(false))
              return false
            }}
          >
            <p className="ant-upload-drag-icon">
              <InboxOutlined />
            </p>
            <p className="ant-upload-text">Excel faylını bura sürükləyin və ya seçin</p>
            <p className="ant-upload-hint">Tələb olunan sheet-lər: Company, Sites, Workers, Assignments, WorkPhases.</p>
          </Upload.Dragger>
          {parsing ? <Alert type="info" showIcon message="Excel faylı yoxlanılır..." /> : null}
        </section>

        <aside className="panel-card export-panel">
          <h2>Demo data idarəetməsi</h2>
          <p>Cari mənbə: <strong>{data?.source === 'imported' ? 'Excel import' : 'Sample data'}</strong></p>
          <ToolbarButton
            icon={<UploadOutlined />}
            tone="green"
            onClick={() => {
              if (!result?.valid) {
                void message.warning('Əvvəlcə düzgün Excel faylı seçin.')
                return
              }
              void saveImportedData(result.data).then(() => message.success('Import yadda saxlanıldı və demo hesabatlar yeniləndi'))
            }}
          >
            Importu yadda saxla
          </ToolbarButton>
          <ToolbarButton icon={<ReloadOutlined />} onClick={() => void generateSampleData().then(() => message.success('Sample data yaradıldı'))}>
            Generate Sample Data
          </ToolbarButton>
          <Button danger onClick={() => void resetDemoData().then(() => message.success('Demo data reset edildi'))}>Reset Demo Data</Button>
        </aside>
      </section>

      {result ? <ExcelImportPreview result={result} /> : null}
    </div>
  )
}
