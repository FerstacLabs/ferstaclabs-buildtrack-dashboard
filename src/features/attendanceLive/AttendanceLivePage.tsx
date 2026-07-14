import { ClockCircleOutlined, LoginOutlined, LogoutOutlined, ReloadOutlined, TeamOutlined } from '@ant-design/icons'
import { Alert, Select, Space, Table, Tag } from 'antd'
import type { TableColumnsType } from 'antd'
import { useEffect, useMemo, useState } from 'react'
import { KpiCard } from '../../components/ui/KpiCard'
import { PageTitle } from '../../components/ui/PageTitle'
import { ToolbarButton } from '../../components/ui/ToolbarButton'
import { buildTrackBackendApi, type AttendanceDailySummary, type AttendanceLiveStatus, type AttendanceSessionRow, type BackendSite } from '../../services/api/buildTrackBackendApi'

const API_TEST_SITE_ID = 'c235fd3e-2f5b-4cac-bb1d-92a94dd54b23'
const API_TEST_SITE_NAME = 'API Test Obyekti'

const statusColor: Record<string, string> = {
  Open: 'green',
  Closed: 'blue',
}

const statusLabel: Record<string, string> = {
  Open: 'İşdə qeydiyyatda',
  Closed: 'Təsdiqli çıxış',
}

const showDebug = import.meta.env.DEV || import.meta.env.VITE_SHOW_ATTENDANCE_DEBUG === 'true'

const formatDuration = (minutes: number) => {
  const safeMinutes = Math.max(0, Math.floor(minutes))
  const hours = Math.floor(safeMinutes / 60)
  const rest = safeMinutes % 60
  if (hours === 0) return `${rest} dəq`
  return `${hours}s ${rest}dəq`
}

const bakuIsoDate = (date = new Date()) => {
  const parts = new Intl.DateTimeFormat('en-US', {
    timeZone: 'Asia/Baku',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  }).formatToParts(date)

  const year = parts.find((part) => part.type === 'year')?.value ?? `${date.getUTCFullYear()}`
  const month = parts.find((part) => part.type === 'month')?.value ?? `${date.getUTCMonth() + 1}`.padStart(2, '0')
  const day = parts.find((part) => part.type === 'day')?.value ?? `${date.getUTCDate()}`.padStart(2, '0')
  return `${year}-${month}-${day}`
}

const resolveInitialSiteId = (siteRows: BackendSite[], currentSiteId?: string) => {
  if (currentSiteId) return currentSiteId
  const apiTestSite = siteRows.find((site) => site.id === API_TEST_SITE_ID) ?? siteRows.find((site) => site.name === API_TEST_SITE_NAME)
  if (apiTestSite) return apiTestSite.id === API_TEST_SITE_ID ? apiTestSite.id : API_TEST_SITE_ID
  return siteRows[0]?.id
}

export const AttendanceLivePage = () => {
  const [sites, setSites] = useState<BackendSite[]>([])
  const [siteId, setSiteId] = useState<string>()
  const [summary, setSummary] = useState<AttendanceDailySummary>()
  const [liveStatus, setLiveStatus] = useState<AttendanceLiveStatus>()
  const [requestedSiteId, setRequestedSiteId] = useState('')
  const [requestedDate, setRequestedDate] = useState('')
  const [securityEventsCount, setSecurityEventsCount] = useState(0)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')

  const loadSites = async () => {
    try {
      const siteRows = await buildTrackBackendApi.getSites()
      setSites(siteRows)
      const initialSiteId = resolveInitialSiteId(siteRows, siteId)
      if (initialSiteId && initialSiteId !== siteId) setSiteId(initialSiteId)
    } catch (err) {
      setError(err instanceof Error ? `${err.message}. API: ${buildTrackBackendApi.baseUrl}` : 'Backend ilə əlaqə alınmadı')
    }
  }

  const loadSessions = async (selectedSiteId = siteId) => {
    if (!selectedSiteId) return
    setLoading(true)
    setError('')
    setRequestedSiteId(selectedSiteId)
    try {
      const status = await buildTrackBackendApi.getAttendanceLiveStatus(selectedSiteId)
      setLiveStatus(status)
      const date = status.workDate ?? bakuIsoDate()
      setRequestedDate(date)
      const dailySummary = await buildTrackBackendApi.getAttendanceDaily(selectedSiteId, date)
      setSummary(dailySummary)
      try {
        const securityEvents = await buildTrackBackendApi.getSecurityEvents(selectedSiteId, date)
        setSecurityEventsCount(securityEvents.length)
      } catch {
        setSecurityEventsCount(0)
      }
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Canlı davamiyyət sessiyaları yüklənmədi'
      setError(`${message}. API: ${buildTrackBackendApi.baseUrl}`)
      setLiveStatus(undefined)
      setSummary(undefined)
      setSecurityEventsCount(0)
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    void loadSites()
  }, [])

  useEffect(() => {
    void loadSessions(siteId)
    if (!siteId) return
    const timer = window.setInterval(() => void loadSessions(siteId), 5000)
    return () => window.clearInterval(timer)
  }, [siteId])

  useEffect(() => {
    const refreshAfterSimulatorEvent = (event: Event) => {
      const customEvent = event as CustomEvent<{ siteId?: string }>
      if (!customEvent.detail?.siteId || customEvent.detail.siteId === siteId) void loadSessions(customEvent.detail?.siteId ?? siteId)
    }

    window.addEventListener('buildtrack:attendance-event-created', refreshAfterSimulatorEvent)
    return () => window.removeEventListener('buildtrack:attendance-event-created', refreshAfterSimulatorEvent)
  }, [siteId])

  const siteNameById = useMemo(() => new Map(sites.map((site) => [site.id, site.name])), [sites])
  const siteOptions = useMemo(() => {
    const options = sites.map((site) => ({ label: site.name, value: site.id }))
    if (!options.some((option) => option.value === API_TEST_SITE_ID)) options.unshift({ label: API_TEST_SITE_NAME, value: API_TEST_SITE_ID })
    return options
  }, [sites])

  const liveWorkerRows: AttendanceSessionRow[] = (liveStatus?.workers ?? []).map((worker) => ({
    id: `live-${worker.workerExternalId}-${worker.checkInTime}`,
    workerExternalId: worker.workerExternalId,
    workerName: worker.workerName,
    checkInTime: worker.checkInTime,
    checkOutTime: worker.confirmedCheckOutTime,
    checkInTimeLocal: worker.checkInTimeLocal,
    checkOutTimeLocal: worker.confirmedCheckOutTimeLocal,
    lastSeenTime: worker.lastSeenTime,
    lastSeenTimeLocal: worker.lastSeenTimeLocal,
    confirmedCheckOutTime: worker.confirmedCheckOutTime,
    confirmedCheckOutTimeLocal: worker.confirmedCheckOutTimeLocal,
    closeReason: worker.closeReason,
    displayStatus: worker.displayStatus,
    isCheckoutConfirmed: worker.isCheckoutConfirmed,
    workedMinutes: worker.workedMinutesSoFar,
    status: worker.status,
    source: 'attendance_live_status',
  }))
  const sessions = summary?.sessions.length ? summary.sessions : liveWorkerRows
  const activeWorkersCount = liveStatus?.activeWorkersCount ?? summary?.activeWorkersCount ?? 0
  const totalCheckedIn = summary?.totalWorkersCheckedIn || sessions.length
  const confirmedCheckoutCount = sessions.filter((session) => session.isCheckoutConfirmed).length
  const totalWorkedHours = summary?.totalWorkedHours ?? Math.round((sessions.reduce((sum, session) => sum + session.workedMinutes, 0) / 60) * 10) / 10

  const columns: TableColumnsType<AttendanceSessionRow> = [
    { title: 'İşçi', dataIndex: 'workerName', render: (value, row) => value ?? row.workerExternalId },
    { title: 'Worker ID', dataIndex: 'workerExternalId' },
    { title: 'İlk görünmə', dataIndex: 'checkInTimeLocal', sorter: (a, b) => a.checkInTime.localeCompare(b.checkInTime) },
    { title: 'Son görülmə', dataIndex: 'lastSeenTimeLocal', render: (value, row) => value ?? row.checkInTimeLocal ?? '-' },
    { title: 'Təsdiqli çıxış', dataIndex: 'confirmedCheckOutTimeLocal', render: (value) => value ?? 'Yoxdur' },
    { title: 'Status', dataIndex: 'displayStatus', render: (value, row) => <Tag color={statusColor[row.status] ?? 'default'}>{value ?? statusLabel[row.status] ?? row.status}</Tag> },
    { title: 'İş müddəti', dataIndex: 'workedMinutes', sorter: (a, b) => a.workedMinutes - b.workedMinutes, render: (value) => formatDuration(value) },
    { title: 'Mənbə', dataIndex: 'source', render: (value) => <Tag color={String(value).includes('cgi') ? 'cyan' : 'purple'}>{value}</Tag> },
  ]

  return (
    <div className="page-stack">
      <PageTitle title="Canlı Davamiyyət" subtitle="Bir kamera rejimində tanınmalar giriş-çıxış deyil, ilk görünmə və son görülmə kimi izlənir" />

      {error && <Alert type="error" showIcon message="Canlı davamiyyət yüklənmədi" description={error} />}

      <Alert
        type="info"
        showIcon
        message="Biznes davamiyyət görünüşü"
        description="Bir Dahua terminalı giriş və çıxışı ayırd edə bilmədiyi üçün təkrar tanınmalar checkout yaratmır. Onlar işçinin son görülmə vaxtını yeniləyir."
      />

      <section className="filter-bar live-filter-bar">
        <Select
          value={siteId}
          onChange={setSiteId}
          options={siteOptions}
          placeholder="Obyekt seçin"
        />
        <ToolbarButton icon={<ReloadOutlined />} onClick={() => loadSessions()}>Yenilə</ToolbarButton>
        {siteId && <Tag color="blue">Obyekt: {siteNameById.get(siteId) ?? (siteId === API_TEST_SITE_ID ? API_TEST_SITE_NAME : siteId)}</Tag>}
      </section>

      {showDebug && (
        <Alert
          type="warning"
          showIcon
          message="Live Attendance debug"
          description={`requestedSiteId=${requestedSiteId || '-'} | requestedDate=${requestedDate || '-'} | liveStatus.activeWorkersCount=${liveStatus?.activeWorkersCount ?? '-'} | liveStatus.workers.length=${liveStatus?.workers.length ?? '-'} | daily.sessions.length=${summary?.sessions.length ?? '-'} | securityEvents.length=${securityEventsCount}`}
        />
      )}

      <section className="kpi-grid">
        <KpiCard icon={<TeamOutlined />} title="Aktiv işçi" value={activeWorkersCount.toString()} trend="hazırda içəridə" tone="green" />
        <KpiCard icon={<LoginOutlined />} title="Bugün görünən" value={totalCheckedIn.toString()} trend={summary?.workDate || requestedDate || bakuIsoDate()} tone="blue" />
        <KpiCard icon={<LogoutOutlined />} title="Təsdiqli çıxış" value={confirmedCheckoutCount.toString()} trend="exit cihazı/manual" tone="orange" />
        <KpiCard icon={<ClockCircleOutlined />} title="Toplam saat" value={totalWorkedHours.toFixed(1)} trend="bugünkü işlənmiş saat" tone="purple" />
      </section>

      <section className="table-card">
        <div className="card-heading">
          <h2>Bugünkü görünmə sessiyaları</h2>
          <Space>{(summary || liveStatus) && <Tag color="green">Yenilənib: {new Date().toLocaleTimeString()}</Tag>}</Space>
        </div>
        <Table<AttendanceSessionRow>
          columns={columns}
          dataSource={sessions}
          loading={loading}
          rowKey="id"
          pagination={{ pageSize: 12 }}
          scroll={{ x: 'max-content' }}
          locale={{ emptyText: error ? 'API xətası var. Yuxarıdakı xətanı yoxlayın.' : 'Bu tarix üçün davamiyyət sessiyası tapılmadı. Real Dahua CGI event gəldikdən sonra burada giriş sessiyası görünəcək.' }}
        />
      </section>
    </div>
  )
}
