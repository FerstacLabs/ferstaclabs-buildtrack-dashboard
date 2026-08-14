import { Button, DatePicker, Drawer, Select, Space } from 'antd'
import dayjs from 'dayjs'
import { useEffect, useState } from 'react'
import type { BuildTrackData } from '../../types/models'
import type { ReportFilters } from '../../types/reports'
import { defaultFilters, useBuildTrackStore } from '../../services/data/dataService'
import { getFilterOptions } from '../../services/data/reportCalculations'

export type AdvancedFilterField =
  | 'dateRange'
  | 'siteId'
  | 'brigade'
  | 'position'
  | 'status'
  | 'riskLevel'
  | 'entryMethod'
  | 'supervisor'
  | 'exportStatus'
  | 'reportType'

interface AdvancedFilterDrawerProps {
  data: BuildTrackData
  fields: AdvancedFilterField[]
  open: boolean
  onClose: () => void
}

const statusOptions = [
  { label: 'Bütün statuslar', value: 'all' },
  { label: 'Gəlib', value: 'Gəlib' },
  { label: 'Gəlməyib', value: 'Gəlməyib' },
  { label: 'Gecikib', value: 'Gecikib' },
  { label: 'Erkən çıxıb', value: 'Erkən çıxıb' },
  { label: 'Aktiv', value: 'Aktiv' },
  { label: 'Qaralama', value: 'Qaralama' },
]

const riskOptions = [
  { label: 'Bütün risk səviyyələri', value: 'all' },
  { label: 'Aşağı', value: 'Aşağı' },
  { label: 'Orta', value: 'Orta' },
  { label: 'Yüksək', value: 'Yüksək' },
  { label: 'Kritik', value: 'Kritik' },
]

const entryMethodOptions = [
  { label: 'Bütün giriş metodları', value: 'all' },
  { label: 'Mobil App', value: 'Mobil App' },
  { label: 'Turniket', value: 'Turniket' },
  { label: 'Prorab Tablet', value: 'Prorab Tablet' },
  { label: 'Manual', value: 'Manual' },
  { label: 'Offline', value: 'Offline' },
]

const exportStatusOptions = [
  { label: 'Bütün export statusları', value: 'all' },
  { label: 'Hazır', value: 'Hazır' },
  { label: 'Xəta', value: 'Xəta' },
  { label: 'Xəbərdarlıq', value: 'Xəbərdarlıq' },
  { label: 'Göndərilib', value: 'Göndərilib' },
]

const reportTypeOptions = [
  { label: 'Bütün hesabat növləri', value: 'all' },
  { label: 'Davamiyyət', value: 'attendance' },
  { label: 'Maaş', value: 'payroll' },
  { label: 'Risk', value: 'risk' },
  { label: 'Audit', value: 'audit' },
  { label: 'Saatlar', value: 'hours' },
  { label: 'Performans', value: 'performance' },
  { label: '1C Export', value: 'export' },
]

export const AdvancedFilterDrawer = ({ data, fields, onClose, open }: AdvancedFilterDrawerProps) => {
  const { filters, setFilter } = useBuildTrackStore()
  const [draft, setDraft] = useState<ReportFilters>(filters)
  const options = getFilterOptions(data)

  useEffect(() => {
    if (open) setDraft(filters)
  }, [filters, open])

  const setDraftFilter = <K extends keyof ReportFilters>(key: K, value: ReportFilters[K]) => {
    setDraft((current) => ({ ...current, [key]: value }))
  }

  const applyFilters = () => {
    fields.forEach((field) => setFilter(field, draft[field]))
    onClose()
  }

  const resetFilters = () => {
    const next = { ...draft }
    fields.forEach((field) => {
      next[field] = defaultFilters[field] as never
      setFilter(field, defaultFilters[field])
    })
    setDraft(next)
  }

  return (
    <Drawer
      title="Daha çox filtr"
      placement="right"
      width={390}
      open={open}
      onClose={onClose}
      className="advanced-filter-drawer"
      footer={
        <Space className="drawer-actions">
          <Button onClick={resetFilters}>Reset</Button>
          <Button type="primary" onClick={applyFilters}>
            Tətbiq et
          </Button>
        </Space>
      }
    >
      <div className="advanced-filter-stack">
        {fields.includes('dateRange') ? (
          <label>
            Tarix aralığı
            <DatePicker.RangePicker
              allowClear={false}
              value={[dayjs(draft.dateRange[0]), dayjs(draft.dateRange[1])]}
              format="DD.MM.YYYY"
              onChange={(value) => {
                if (value?.[0] && value[1]) {
                  setDraftFilter('dateRange', [value[0].format('YYYY-MM-DD'), value[1].format('YYYY-MM-DD')])
                }
              }}
            />
          </label>
        ) : null}

        {fields.includes('siteId') ? (
          <label>
            Layihə
            <Select
              value={draft.siteId}
              onChange={(value) => setDraftFilter('siteId', value)}
              options={[{ label: 'Bütün layihələr', value: 'all' }, ...options.sites]}
            />
          </label>
        ) : null}

        {fields.includes('brigade') ? (
          <label>
            Briqada
            <Select
              value={draft.brigade}
              onChange={(value) => setDraftFilter('brigade', value)}
              options={[{ label: 'Bütün briqadalar', value: 'all' }, ...options.brigades]}
            />
          </label>
        ) : null}

        {fields.includes('position') ? (
          <label>
            Vəzifə
            <Select
              value={draft.position}
              onChange={(value) => setDraftFilter('position', value)}
              options={[{ label: 'Bütün vəzifələr', value: 'all' }, ...options.positions]}
            />
          </label>
        ) : null}

        {fields.includes('status') ? (
          <label>
            Status
            <Select value={draft.status} onChange={(value) => setDraftFilter('status', value)} options={statusOptions} />
          </label>
        ) : null}

        {fields.includes('riskLevel') ? (
          <label>
            Risk səviyyəsi
            <Select value={draft.riskLevel} onChange={(value) => setDraftFilter('riskLevel', value)} options={riskOptions} />
          </label>
        ) : null}

        {fields.includes('entryMethod') ? (
          <label>
            Giriş metodu
            <Select value={draft.entryMethod} onChange={(value) => setDraftFilter('entryMethod', value)} options={entryMethodOptions} />
          </label>
        ) : null}

        {fields.includes('supervisor') ? (
          <label>
            Prorab
            <Select
              value={draft.supervisor}
              onChange={(value) => setDraftFilter('supervisor', value)}
              options={[{ label: 'Bütün prorablar', value: 'all' }, ...options.supervisors]}
            />
          </label>
        ) : null}

        {fields.includes('exportStatus') ? (
          <label>
            Export statusu
            <Select value={draft.exportStatus} onChange={(value) => setDraftFilter('exportStatus', value)} options={exportStatusOptions} />
          </label>
        ) : null}

        {fields.includes('reportType') ? (
          <label>
            Hesabat növü
            <Select value={draft.reportType} onChange={(value) => setDraftFilter('reportType', value)} options={reportTypeOptions} />
          </label>
        ) : null}
      </div>
    </Drawer>
  )
}
