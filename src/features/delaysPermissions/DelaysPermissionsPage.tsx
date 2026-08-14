import { useEffect, useMemo, useState } from 'react'
import { CheckCircleOutlined, ClockCircleOutlined, DownloadOutlined, LoginOutlined, QuestionCircleOutlined } from '@ant-design/icons'
import type { TableColumnsType } from 'antd'
import { Alert, DatePicker, Space, Spin } from 'antd'
import dayjs, { type Dayjs } from 'dayjs'
import { LineChartCard } from '../../components/charts/LineChartCard'
import { ProjectSelect } from '../../components/ProjectSelect'
import { DataTable } from '../../components/tables/DataTable'
import { ExplanationCard } from '../../components/ui/ExplanationCard'
import { KpiCard } from '../../components/ui/KpiCard'
import { ToolbarButton } from '../../components/ui/ToolbarButton'
import { PageTitle } from '../../components/ui/PageTitle'
import { buildTrackBackendApi, type AttendanceDisciplineReport, type AttendanceDisciplineRow } from '../../services/api/buildTrackBackendApi'
import { exportRowsToCsv, exportRowsToExcel } from '../../services/data/exportService'
import { ALL_PROJECTS_ID, useProjectSelectionStore } from '../../stores/projectSelectionStore'
import { formatNumber, formatPercent } from '../../utils/formatters'
import type { ChartPoint } from '../../types/reports'

const { RangePicker } = DatePicker

const defaultRange = (): [Dayjs, Dayjs] => [dayjs().startOf('month'), dayjs()]
const toApiDate = (value?: Dayjs | null) => value ? value.format('YYYY-MM-DD') : undefined

export const DelaysPermissionsPage = () => {
  const selectedObjectId = useProjectSelectionStore((state) => state.selectedProjectId)
  const [range, setRange] = useState<[Dayjs, Dayjs]>(defaultRange)
  const [report, setReport] = useState<AttendanceDisciplineReport | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const siteId = selectedObjectId === ALL_PROJECTS_ID ? undefined : selectedObjectId
  const dateFrom = toApiDate(range[0])
  const dateTo = toApiDate(range[1])

  useEffect(() => {
    let cancelled = false

    const load = async () => {
      setLoading(true)
      setError(null)
      try {
        const nextReport = await buildTrackBackendApi.getAttendanceDiscipline({ siteId, dateFrom, dateTo })
        if (cancelled) return
        setReport(nextReport)
      } catch (loadError) {
        if (cancelled) return
        console.error('Attendance discipline backend load failed', loadError)
        setError('Backend davamiyyət məlumatı yüklənmədi')
      } finally {
        if (!cancelled) setLoading(false)
      }
    }

    void load()
    return () => {
      cancelled = true
    }
  }, [dateFrom, dateTo, siteId])

  const rows = report?.rows ?? []
  const trend = useMemo<ChartPoint[]>(() => (report?.trend ?? []).map((point) => ({
    name: point.label,
    gecikmə: point.lateCount,
    saat: point.lateHours,
    erkən: point.earlyExitCount,
  })), [report])

  const columns: TableColumnsType<AttendanceDisciplineRow> = [
    { title: 'İşçi adı', dataIndex: 'workerName', sorter: (a, b) => a.workerName.localeCompare(b.workerName) },
    { title: 'Layihə', dataIndex: 'siteName' },
    { title: 'Vəzifə', dataIndex: 'role', render: (value?: string) => value || '—' },
    { title: 'Briqada', dataIndex: 'brigade', render: (value?: string) => value || '—' },
    { title: 'Gecikmə Sayı', dataIndex: 'lateCount', sorter: (a, b) => a.lateCount - b.lateCount },
    { title: 'Ümumi Gecikmə', dataIndex: 'totalLateMinutes', sorter: (a, b) => a.totalLateMinutes - b.totalLateMinutes, render: (value: number) => `${formatNumber(value)} dəq` },
    { title: 'Erkən Çıxış Sayı', dataIndex: 'earlyExitCount', sorter: (a, b) => a.earlyExitCount - b.earlyExitCount },
    { title: 'İcazə Saat/Gün', render: (_, row) => `${formatNumber(row.approvedPermissionHours, 1)} saat / ${formatNumber(row.approvedPermissionDays)} gün` },
    { title: 'Davamiyyət %', dataIndex: 'attendancePercent', sorter: (a, b) => a.attendancePercent - b.attendancePercent, render: (value: number) => formatPercent(value) },
    { title: 'Trend', dataIndex: 'trend', render: (value: string) => <span className={`trend-pill trend-${value === 'Diqqət' ? 'down' : 'stable'}`}>{value}</span> },
    { title: 'Qeyd', dataIndex: 'note' },
  ]

  return (
    <div className="page-stack">
      <PageTitle
        title="4. Gecikmə, Erkən Çıxış və İcazələr"
        extra={
          <Space wrap>
            <RangePicker
              allowClear={false}
              format="DD.MM.YYYY"
              value={range}
              onChange={(value) => {
                if (value?.[0] && value[1]) setRange([value[0], value[1]])
              }}
            />
            <ProjectSelect pageKey="delays" />
          </Space>
        }
      />

      {error ? <Alert type="error" showIcon message={error} /> : null}
      {report && !report.permissionDomainAvailable ? (
        <Alert type="info" showIcon message="İcazə/leave üçün ayrıca canonical domain hələ aktiv deyil; bu hesabatda icazə göstəriciləri 0 kimi saxlanılır." />
      ) : null}
      {loading && !report ? (
        <section className="panel-card">
          <Spin /> <span style={{ marginLeft: 12 }}>Gecikmə hesabatı yüklənir...</span>
        </section>
      ) : null}

      {report ? (
        <>
          <section className="kpi-grid">
            <KpiCard icon={<ClockCircleOutlined />} title="Gecikmə Sayı" value={formatNumber(report.lateCount)} trend={`${report.lateGraceMinutes} dəq grace`} tone="green" />
            <KpiCard icon={<ClockCircleOutlined />} title="Ümumi Gecikmə Dəq" value={formatNumber(report.totalLateMinutes)} trend="real check-in vaxtı" tone="blue" />
            <KpiCard icon={<LoginOutlined />} title="Erkən Çıxış" value={formatNumber(report.earlyExitCount)} trend={`${report.earlyExitGraceMinutes} dəq grace`} tone="orange" />
            <KpiCard icon={<CheckCircleOutlined />} title="Davamiyyət Faizi" value={formatPercent(report.attendancePercent)} trend={`${formatNumber(report.presentWorkerDays)} / ${formatNumber(report.scheduledWorkerDays)} işçi-gün`} tone="green" />
          </section>

          <DataTable
            title="Gecikmə və İcazə Hesabatı"
            columns={columns}
            data={rows}
            emptyText="Bu period üçün gecikmə, erkən çıxış və ya gəlməmə göstəricisi tapılmadı"
            extra={
              <div className="table-actions">
                <ToolbarButton icon={<DownloadOutlined />} onClick={() => exportRowsToExcel('gecikme-icaze', rows)}>Excel Export</ToolbarButton>
                <ToolbarButton icon={<DownloadOutlined />} tone="purple" onClick={() => exportRowsToCsv('gecikme-icaze', rows)}>CSV Export</ToolbarButton>
              </div>
            }
          />
          <LineChartCard
            title="Gecikmə və Erkən Çıxış Trendi"
            data={trend}
            lines={[
              { dataKey: 'gecikmə', color: '#078b55', name: 'Gecikmə Sayı' },
              { dataKey: 'saat', color: '#1479ff', name: 'Gecikmə Saatı' },
              { dataKey: 'erkən', color: '#ff8a00', name: 'Erkən Çıxış Sayı' },
            ]}
          />
        </>
      ) : null}

      <section className="explanation-grid">
        <ExplanationCard icon={<QuestionCircleOutlined />} title="Bu tablo niyə lazımdır?">
          <p>İşçilərin zaman intizamı real AttendanceSessions əsasında izlənir; gecikmə riskScore-dan deyil, faktiki giriş vaxtından hesablanır.</p>
        </ExplanationCard>
        <ExplanationCard icon={<ClockCircleOutlined />} title="Custom imkanlar" tone="orange">
          <ul>
            <li>Layihə və tarix aralığı üzrə filtr.</li>
            <li>Gecikmə = faktiki giriş - plan giriş - grace.</li>
            <li>Erkən çıxış = plan çıxış - təsdiqli çıxış - grace.</li>
          </ul>
        </ExplanationCard>
      </section>
    </div>
  )
}
