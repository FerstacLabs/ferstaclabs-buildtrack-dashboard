import { useEffect, useMemo, useState } from 'react'
import { ClockCircleOutlined, DownloadOutlined, TeamOutlined, UserDeleteOutlined } from '@ant-design/icons'
import type { TableColumnsType } from 'antd'
import { Alert, DatePicker, Space, Spin, Tag } from 'antd'
import dayjs, { type Dayjs } from 'dayjs'
import { DonutChartCard } from '../../components/charts/DonutChartCard'
import { ProjectSelect } from '../../components/ProjectSelect'
import { DataTable } from '../../components/tables/DataTable'
import { ExplanationCard } from '../../components/ui/ExplanationCard'
import { KpiCard } from '../../components/ui/KpiCard'
import { PageTitle } from '../../components/ui/PageTitle'
import { RiskBadge } from '../../components/ui/RiskBadge'
import { ToolbarButton } from '../../components/ui/ToolbarButton'
import { buildTrackBackendApi, type AttendanceDailyRosterReport, type AttendanceDailyRosterRow } from '../../services/api/buildTrackBackendApi'
import { exportRowsToExcel } from '../../services/data/exportService'
import { ALL_PROJECTS_ID } from '../../stores/projectSelectionStore'
import { useProjectSelectionStore } from '../../stores/projectSelectionStore'
import { formatHours, formatNumber, formatPercent } from '../../utils/formatters'

const statusColor: Record<AttendanceDailyRosterRow['status'], string> = {
  Gəlib: 'green',
  Gecikib: 'orange',
  Gəlməyib: 'red',
  Riskli: 'red',
}

const toApiDate = (value?: Dayjs | null) => value ? value.format('YYYY-MM-DD') : undefined

export const DailyAttendancePage = () => {
  const selectedObjectId = useProjectSelectionStore((state) => state.selectedProjectId)
  const [selectedDate, setSelectedDate] = useState<Dayjs | null>(null)
  const [report, setReport] = useState<AttendanceDailyRosterReport | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const siteId = selectedObjectId === ALL_PROJECTS_ID ? undefined : selectedObjectId
  const requestedDate = toApiDate(selectedDate)

  useEffect(() => {
    let cancelled = false

    const load = async () => {
      setLoading(true)
      setError(null)
      try {
        const nextReport = await buildTrackBackendApi.getAttendanceDailyRoster({ siteId, date: requestedDate })
        if (cancelled) return
        setReport(nextReport)
        if (!selectedDate && nextReport.workDate) {
          setSelectedDate(dayjs(nextReport.workDate))
        }
      } catch (loadError) {
        if (cancelled) return
        console.error('Daily attendance backend load failed', loadError)
        setError('Backend davamiyyət məlumatı yüklənmədi')
      } finally {
        if (!cancelled) setLoading(false)
      }
    }

    void load()
    return () => {
      cancelled = true
    }
  }, [requestedDate, selectedDate, siteId])

  const rows = report?.rows ?? []
  const total = Math.max(1, report?.activeWorkersCount ?? 0)
  const donut = useMemo(() => [
    { name: 'Gəlib', value: report?.presentCount ?? 0 },
    { name: 'Gəlməyib', value: report?.absentCount ?? 0 },
    { name: 'Gecikib', value: report?.lateCount ?? 0 },
    { name: 'Erkən çıxış', value: report?.earlyExitCount ?? 0 },
  ], [report])

  const columns: TableColumnsType<AttendanceDailyRosterRow> = [
    { title: 'İşçi ID', dataIndex: 'workerExternalId', sorter: (a, b) => a.workerExternalId.localeCompare(b.workerExternalId) },
    { title: 'İşçi adı', dataIndex: 'workerName', sorter: (a, b) => a.workerName.localeCompare(b.workerName) },
    { title: 'Layihə', dataIndex: 'siteName' },
    { title: 'Vəzifə', dataIndex: 'role', render: (value?: string) => value || '—' },
    { title: 'Briqada', dataIndex: 'brigade', render: (value?: string) => value || '—' },
    { title: 'Plan Giriş', dataIndex: 'plannedCheckIn' },
    { title: 'Faktiki Giriş', dataIndex: 'actualCheckInLocal', render: (value?: string) => value || '—' },
    { title: 'Plan Çıxış', dataIndex: 'plannedCheckOut' },
    { title: 'Faktiki Çıxış', dataIndex: 'actualCheckOutLocal', render: (value?: string) => value || '—' },
    { title: 'Status', dataIndex: 'status', render: (status: AttendanceDailyRosterRow['status']) => <Tag color={statusColor[status]}>{status}</Tag> },
    { title: 'Gecikmə', dataIndex: 'lateMinutes', sorter: (a, b) => a.lateMinutes - b.lateMinutes, render: (value: number) => `${formatNumber(value)} dəq` },
    { title: 'İşlənmiş Saat', dataIndex: 'workedHours', sorter: (a, b) => a.workedHours - b.workedHours, render: (value: number) => formatHours(value, 1) },
    { title: 'Giriş Metodu', dataIndex: 'entryMethod' },
    { title: 'Risk', dataIndex: 'riskScore', render: (_: number, row) => <RiskBadge level={row.riskLevel} score={row.riskScore} /> },
  ]

  return (
    <div className="page-stack">
      <PageTitle
        title="1. Günlük Davamiyyət Paneli"
        extra={
          <Space wrap>
            <DatePicker
              allowClear={false}
              format="DD.MM.YYYY"
              value={selectedDate}
              onChange={(value) => setSelectedDate(value)}
              placeholder="Tarix"
            />
            <ProjectSelect pageKey="attendance" />
          </Space>
        }
      />

      {error ? <Alert type="error" showIcon message={error} /> : null}
      {loading && !report ? (
        <section className="panel-card">
          <Spin /> <span style={{ marginLeft: 12 }}>Davamiyyət məlumatı yüklənir...</span>
        </section>
      ) : null}

      {report ? (
        <>
          <section className="kpi-grid">
            <KpiCard icon={<TeamOutlined />} title="Bugün Gələn" value={formatNumber(report.presentCount)} trend={formatPercent((report.presentCount / total) * 100)} tone="green" />
            <KpiCard icon={<UserDeleteOutlined />} title="Gəlməyən" value={formatNumber(report.absentCount)} trend={formatPercent((report.absentCount / total) * 100)} tone="red" />
            <KpiCard icon={<ClockCircleOutlined />} title="Gecikən" value={formatNumber(report.lateCount)} trend={`${report.lateGraceMinutes} dəq grace`} tone="orange" />
            <KpiCard icon={<ClockCircleOutlined />} title="Aktiv Saat" value={formatHours(report.totalWorkedHours, 1)} trend="canonical sessiyalar üzrə" tone="blue" />
          </section>

          <DataTable
            title="Günlük Davamiyyət Siyahısı"
            columns={columns}
            data={rows}
            emptyText="Bu tarix və layihə üçün davamiyyət rosteri tapılmadı"
            extra={<ToolbarButton icon={<DownloadOutlined />} onClick={() => exportRowsToExcel('gunluk-davamiyyet', rows)}>Export</ToolbarButton>}
          />

          <section className="daily-summary-grid">
            <DonutChartCard title="Status üzrə xülasə" data={donut} centerValue={formatNumber(report.activeWorkersCount)} centerLabel="cəmi nəfər" height={220} />
            <aside className="panel-card daily-insight-card">
              <h2>Günün xülasəsi</h2>
              <div className="summary-metric"><span>Davamiyyət faizi</span><strong>{formatPercent(report.attendancePercent)}</strong></div>
              <div className="summary-metric"><span>Erkən çıxış</span><strong>{formatNumber(report.earlyExitCount)}</strong></div>
              <div className="summary-metric"><span>Plan saatı</span><strong>{report.plannedStart} - {report.plannedEnd}</strong></div>
              <p>Gecikmə hesabı faktiki giriş vaxtından plan giriş və {report.lateGraceMinutes} dəqiqə grace çıxılaraq hesablanır.</p>
            </aside>
          </section>
        </>
      ) : null}

      <section className="explanation-grid">
        <ExplanationCard icon={<TeamOutlined />} title="Bu tablo niyə lazımdır?">
          <ul>
            <li>Günlük roster backend Workers, Sites və AttendanceSessions cədvəllərindən hesablanır.</li>
            <li>Sessiyası olmayan aktiv işçi ayrıca “Gəlməyib” kimi görünür.</li>
            <li>Maaş hesablamasına gedən iş saatları ilə eyni canonical attendance datası istifadə olunur.</li>
          </ul>
        </ExplanationCard>
        <ExplanationCard icon={<DownloadOutlined />} title="Custom imkanlar" tone="orange">
          <ul>
            <li>Layihə və tarix üzrə filtr.</li>
            <li>Visible rosterin Excel exportu.</li>
          </ul>
        </ExplanationCard>
      </section>
    </div>
  )
}
