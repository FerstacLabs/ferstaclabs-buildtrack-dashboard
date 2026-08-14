import { FilterOutlined } from '@ant-design/icons'
import { Button, DatePicker, Select } from 'antd'
import dayjs from 'dayjs'
import { useMemo, useState } from 'react'
import type { BuildTrackData } from '../../types/models'
import type { ReportFilters } from '../../types/reports'
import { getFilterOptions } from '../../services/data/reportCalculations'
import { useBuildTrackStore } from '../../services/data/dataService'
import { AdvancedFilterDrawer, type AdvancedFilterField } from './AdvancedFilterDrawer'

interface FilterBarProps {
  data: BuildTrackData
  showStatus?: boolean
  showPosition?: boolean
  showRisk?: boolean
  showSupervisor?: boolean
  showMonth?: boolean
  showReportType?: boolean
  advancedFields?: AdvancedFilterField[]
  sitePlaceholder?: string
  brigadePlaceholder?: string
}

const attendanceStatusOptions = [
  { label: 'Bütün Statuslar', value: 'all' },
  { label: 'Gəlib', value: 'Gəlib' },
  { label: 'Gəlməyib', value: 'Gəlməyib' },
  { label: 'Gecikib', value: 'Gecikib' },
  { label: 'Erkən çıxıb', value: 'Erkən çıxıb' },
]

const riskOptions = [
  { label: 'Bütün Risklər', value: 'all' },
  { label: 'Aşağı', value: 'Aşağı' },
  { label: 'Orta', value: 'Orta' },
  { label: 'Yüksək', value: 'Yüksək' },
  { label: 'Kritik', value: 'Kritik' },
]

const reportTypeOptions = [
  { label: 'Bütün Növlər', value: 'all' },
  { label: 'Davamiyyət', value: 'attendance' },
  { label: 'Maaş', value: 'payroll' },
  { label: 'Risk', value: 'risk' },
  { label: 'Audit', value: 'audit' },
]

export const FilterBar = ({
  data,
  showStatus,
  showPosition,
  showRisk,
  showSupervisor,
  showMonth,
  showReportType,
  advancedFields,
  sitePlaceholder = 'Bütün Layihələr',
  brigadePlaceholder = 'Bütün Briqadalar',
}: FilterBarProps) => {
  const { filters, setFilter } = useBuildTrackStore()
  const [drawerOpen, setDrawerOpen] = useState(false)
  const options = getFilterOptions(data)
  const update = <K extends keyof ReportFilters>(key: K, value: ReportFilters[K]) => setFilter(key, value)
  const drawerFields = useMemo<AdvancedFilterField[]>(
    () =>
      advancedFields ?? [
        'dateRange',
        'siteId',
        'brigade',
        ...(showPosition ? (['position'] as const) : []),
        ...(showStatus ? (['status'] as const) : []),
        ...(showRisk ? (['riskLevel'] as const) : []),
        ...(showSupervisor ? (['supervisor'] as const) : []),
        ...(showReportType ? (['reportType'] as const) : []),
      ],
    [advancedFields, showPosition, showReportType, showRisk, showStatus, showSupervisor],
  )

  return (
    <div className="filter-bar">
      {showMonth ? (
        <DatePicker
          picker="month"
          allowClear={false}
          value={dayjs(`${filters.month}-01`)}
          format="MMM YYYY"
          onChange={(value) => {
            if (value) update('month', value.format('YYYY-MM'))
          }}
        />
      ) : (
        <DatePicker.RangePicker
          allowClear={false}
          value={[dayjs(filters.dateRange[0]), dayjs(filters.dateRange[1])]}
          format="DD.MM.YYYY"
          onChange={(value) => {
            if (value?.[0] && value[1]) {
              update('dateRange', [value[0].format('YYYY-MM-DD'), value[1].format('YYYY-MM-DD')])
            }
          }}
        />
      )}

      <Select
        value={filters.siteId}
        onChange={(value) => update('siteId', value)}
        options={[{ label: sitePlaceholder, value: 'all' }, ...options.sites]}
      />

      <Select
        value={filters.brigade}
        onChange={(value) => update('brigade', value)}
        options={[{ label: brigadePlaceholder, value: 'all' }, ...options.brigades]}
      />

      {showPosition ? (
        <Select
          value={filters.position}
          onChange={(value) => update('position', value)}
          options={[{ label: 'Bütün Vəzifələr', value: 'all' }, ...options.positions]}
        />
      ) : null}

      {showStatus ? <Select value={filters.status} onChange={(value) => update('status', value)} options={attendanceStatusOptions} /> : null}

      {showRisk ? <Select value={filters.riskLevel} onChange={(value) => update('riskLevel', value)} options={riskOptions} /> : null}

      {showSupervisor ? (
        <Select
          value={filters.supervisor}
          onChange={(value) => update('supervisor', value)}
          options={[{ label: 'Bütün Prorablar', value: 'all' }, ...options.supervisors]}
        />
      ) : null}

      {showReportType ? (
        <Select value={filters.reportType} onChange={(value) => update('reportType', value)} options={reportTypeOptions} />
      ) : null}

      <Button icon={<FilterOutlined />} onClick={() => setDrawerOpen(true)}>Daha çox filtr</Button>
      <AdvancedFilterDrawer data={data} fields={drawerFields} open={drawerOpen} onClose={() => setDrawerOpen(false)} />
    </div>
  )
}
