import { ClockCircleOutlined, LoginOutlined, LogoutOutlined, ReloadOutlined, TeamOutlined } from '@ant-design/icons'
import { Alert, Modal, Select, Space, Table, Tag, Tooltip, message } from 'antd'
import type { TableColumnsType } from 'antd'
import { useEffect, useState } from 'react'
import { AuthenticatedSnapshotImage } from '../../components/ui/AuthenticatedSnapshotImage'
import { PageTitle } from '../../components/ui/PageTitle'
import { ToolbarButton } from '../../components/ui/ToolbarButton'
import {
  buildTrackBackendApi,
  type AttendanceDailySummary,
  type AttendanceLiveStatus,
  type AttendanceSessionRow,
  type AttendanceSnapshotRow,
  type BackendSite,
} from '../../services/api/buildTrackBackendApi'
import { calculateLiveWorkedMinutes, formatLiveDuration, formatLiveTotalDuration } from './attendanceLiveCalculations'

const debugLive = typeof window !== 'undefined'
  && new URLSearchParams(window.location.search).get('debugLive') === '1'

const showDebug =
  import.meta.env.DEV ||
  import.meta.env.VITE_SHOW_ATTENDANCE_DEBUG === 'true' ||
  debugLive

const statusColor: Record<string, string> = {
  Open: 'green',
  Closed: 'blue',
}

const statusLabel: Record<string, string> = {
  Open: 'İşdə qeydiyyatda',
  Closed: 'Təsdiqli çıxış',
}

const sourceLabel = (source: string) => {
  if (source === 'dahua_active_register') return 'Active Register'
  if (source === 'dahua_cgi_polling') return 'CGI polling'
  if (source === 'attendance_live_status') return 'Live status'
  return source
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
  if (currentSiteId && siteRows.some((site) => site.id === currentSiteId)) return currentSiteId
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
  const [galleryOpen, setGalleryOpen] = useState(false)
  const [galleryLoading, setGalleryLoading] = useState(false)
  const [galleryWorker, setGalleryWorker] = useState<AttendanceSessionRow | null>(null)
  const [gallerySnapshots, setGallerySnapshots] = useState<AttendanceSnapshotRow[]>([])
  const [failedSnapshotRows, setFailedSnapshotRows] = useState<Set<string>>(() => new Set())
  const [nowTick, setNowTick] = useState(() => Date.now())

  const loadSites = async () => {
    try {
      const siteRows = await buildTrackBackendApi.getSites()
      setSites(siteRows)
      const initialSiteId = resolveInitialSiteId(siteRows, siteId)
      if (initialSiteId && initialSiteId !== siteId) setSiteId(initialSiteId)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Backend ilə əlaqə alınmadı')
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
        setSecurityEventsCount(securityEvents.filter((event) => event.status === 'Open').length)
      } catch {
        setSecurityEventsCount(0)
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Canlı davamiyyət sessiyaları yüklənmədi')
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
    const timer = window.setInterval(() => void loadSessions(siteId), 30000)
    return () => window.clearInterval(timer)
  }, [siteId])

  useEffect(() => {
    const timer = window.setInterval(() => setNowTick(Date.now()), 30000)
    return () => window.clearInterval(timer)
  }, [])

  useEffect(() => {
    const refreshAfterSimulatorEvent = (event: Event) => {
      const customEvent = event as CustomEvent<{ siteId?: string }>
      if (!customEvent.detail?.siteId || customEvent.detail.siteId === siteId) {
        void loadSessions(customEvent.detail?.siteId ?? siteId)
      }
    }

    window.addEventListener('buildtrack:attendance-event-created', refreshAfterSimulatorEvent)
    return () => window.removeEventListener('buildtrack:attendance-event-created', refreshAfterSimulatorEvent)
  }, [siteId])

  const siteNameById = new Map(sites.map((site) => [site.id, site.name]))
  const siteOptions = sites.map((site) => ({ label: site.name, value: site.id }))

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

  const visibleSessions = (summary?.sessions.length ? summary.sessions : liveWorkerRows).map((session) => ({
    ...session,
    workedMinutes: calculateLiveWorkedMinutes(session, nowTick),
  }))

  const tableRows = visibleSessions

  const rowTotalMinutes = tableRows.reduce(
    (sum, row) => sum + calculateLiveWorkedMinutes(row, nowTick),
    0,
  )
  const apiTotalMinutes = Math.round(Number(summary?.totalWorkedHours ?? 0) * 60)
  const activeWorkersKpi = Math.max(
    Number(summary?.activeWorkersCount ?? 0),
    Number(liveStatus?.activeWorkersCount ?? 0),
    tableRows.length > 0 ? 1 : 0,
  )
  const todaySeenKpi = Math.max(
    Number(summary?.totalWorkersCheckedIn ?? 0),
    Number(summary?.activeWorkersCount ?? 0),
    Number(liveStatus?.activeWorkersCount ?? 0),
    tableRows.length,
  )
  const confirmedCheckoutsKpi = Number(summary?.closedSessionsCount ?? 0)
  const totalMinutesKpi = Math.max(apiTotalMinutes, rowTotalMinutes)
  const totalDurationKpi = formatLiveTotalDuration(totalMinutesKpi)
  const liveBuildMarker = 'clean-rewrite-live-v4'

  const visibleGallerySnapshots = gallerySnapshots.filter((snapshot) => snapshot.snapshotUrl)

  useEffect(() => {
    if (!showDebug) return
    console.warn('[LiveAttendance DEBUG]', {
      liveStatusResponse: liveStatus,
      dailyResponse: summary,
      tableRows,
      visibleSessionsCount: tableRows.length,
      firstTableRow: tableRows[0],
      nowTick,
    })
  }, [tableRows, nowTick, liveStatus, summary])

  useEffect(() => {
    if (!debugLive) return
    console.warn('[LiveAttendance BUILD]', liveBuildMarker)
  }, [liveBuildMarker])

  const openSnapshotGallery = async (row: AttendanceSessionRow) => {
    if (!siteId || !requestedDate) return
    setGalleryWorker(row)
    setGalleryOpen(true)
    setGalleryLoading(true)
    setGallerySnapshots([])

    try {
      const snapshots = await buildTrackBackendApi.getAttendanceSnapshots(siteId, row.workerExternalId, requestedDate)
      setGallerySnapshots(snapshots)
    } catch (err) {
      setGallerySnapshots([])
      message.error(err instanceof Error ? err.message : 'Snapshot qalereyası yüklənmədi')
    } finally {
      setGalleryLoading(false)
    }
  }

  const closeSnapshotGallery = () => {
    setGalleryOpen(false)
    setGalleryWorker(null)
    setGallerySnapshots([])
  }

  const renderSnapshotThumbnail = (row: AttendanceSessionRow) => {
    if (!row.snapshotUrl || failedSnapshotRows.has(row.id)) {
      return <span className="snapshot-placeholder">Şəkil yoxdur</span>
    }

    return (
      <button type="button" className="snapshot-thumb-button" onClick={() => openSnapshotGallery(row)} aria-label="Davamiyyət şəkillərini aç">
        <AuthenticatedSnapshotImage
          src={buildTrackBackendApi.attendanceSnapshotUrl(row.snapshotUrl)}
          alt="Davamiyyət snapshot"
          width={84}
          height={54}
          className="snapshot-thumb-image"
          placeholder={<span className="snapshot-placeholder">Şəkil yüklənir...</span>}
          onUnavailable={() => {
            setFailedSnapshotRows((current) => {
              const next = new Set(current)
              next.add(row.id)
              return next
            })
          }}
        />
        <span className="snapshot-zoom-label">Qalereya</span>
      </button>
    )
  }

  const columns: TableColumnsType<AttendanceSessionRow> = [
    {
      title: 'İşçi',
      dataIndex: 'workerName',
      render: (value, row) => (
        <Tooltip title={`İşçi kodu: ${row.workerExternalId}`}>
          <strong>{value ?? row.workerExternalId}</strong>
        </Tooltip>
      ),
    },
    { title: 'İlk görünmə', dataIndex: 'checkInTimeLocal', sorter: (a, b) => a.checkInTime.localeCompare(b.checkInTime) },
    { title: 'Son görülmə', dataIndex: 'lastSeenTimeLocal', render: (value, row) => value ?? row.checkInTimeLocal ?? '-' },
    { title: 'Təsdiqli çıxış', dataIndex: 'confirmedCheckOutTimeLocal', render: (value) => value ?? 'Yoxdur' },
    { title: 'Status', dataIndex: 'displayStatus', render: (value, row) => <Tag color={statusColor[row.status] ?? 'default'}>{value ?? statusLabel[row.status] ?? row.status}</Tag> },
    { title: 'Metod', dataIndex: 'method', render: (value) => value ? <Tag color={value === 'Face' ? 'green' : 'blue'}>{value}</Tag> : '-' },
    { title: 'Şəkil', dataIndex: 'snapshotUrl', render: (_, row) => renderSnapshotThumbnail(row) },
    { title: 'İş müddəti', dataIndex: 'workedMinutes', sorter: (a, b) => a.workedMinutes - b.workedMinutes, render: (_, row) => formatLiveDuration(calculateLiveWorkedMinutes(row, nowTick)) },
    { title: 'Mənbə', dataIndex: 'source', render: (value) => <Tag color={String(value).includes('active_register') ? 'purple' : String(value).includes('cgi') ? 'cyan' : 'blue'}>{sourceLabel(String(value))}</Tag> },
  ]

  return (
    <div className="page-stack">
      <PageTitle title="Canlı Davamiyyət" subtitle="Bir kamera rejimində tanınmalar giriş-çıxış deyil, ilk görünmə və son görülmə kimi izlənir" />

      {error && <Alert type="error" showIcon message="Canlı davamiyyət yüklənmədi" description={error} />}

      <Alert
        type="info"
        showIcon
        message="Biznes davamiyyət görünüşü"
        description="Bir kamera terminalı giriş və çıxışı ayırd edə bilmədiyi üçün təkrar tanınmalar checkout yaratmır. Onlar işçinin son görülmə vaxtını yeniləyir."
      />

      {securityEventsCount > 0 && (
        <Alert
          type="warning"
          showIcon
          message="Bu obyekt üzrə yoxlanılmalı kamera hadisələri var"
          description="Şübhəli tanıma və identity mismatch hadisələri davamiyyətə əlavə olunmur. Detallara Tanınmayan üzlər bölməsində baxın."
        />
      )}

      <section className="filter-bar live-filter-bar">
        <Select
          value={siteId}
          onChange={setSiteId}
          options={siteOptions}
          placeholder="Obyekt seçin"
        />
        <ToolbarButton icon={<ReloadOutlined />} onClick={() => loadSessions()}>Yenilə</ToolbarButton>
        {siteId && <Tag color="blue">Obyekt: {siteNameById.get(siteId) ?? siteId}</Tag>}
      </section>

      <section className="kpi-grid" data-build-marker={liveBuildMarker}>
        <div className="kpi-card kpi-green">
          <div className="kpi-top">
            <span className="kpi-icon"><TeamOutlined /></span>
            <span className="kpi-title">Aktiv işçi</span>
          </div>
          <div className="kpi-value" data-testid="live-active-workers">{activeWorkersKpi}</div>
          <div className="kpi-trend">↑ checkout olmayan sessiya</div>
          {showDebug ? (
            <div style={{ color: '#6b7280', fontSize: 11, marginTop: 6 }}>
              <div>build: {liveBuildMarker}</div>
              <div>CLEAN REWRITE V4</div>
              <div>activeWorkersKpi = {activeWorkersKpi}</div>
            </div>
          ) : null}
        </div>

        <div className="kpi-card kpi-blue">
          <div className="kpi-top">
            <span className="kpi-icon"><LoginOutlined /></span>
            <span className="kpi-title">Bugün görünən</span>
          </div>
          <div className="kpi-value" data-testid="live-today-seen">{todaySeenKpi}</div>
          <div className="kpi-trend">↑ {summary?.workDate || requestedDate || bakuIsoDate()}</div>
          {showDebug ? (
            <div style={{ color: '#6b7280', fontSize: 11, marginTop: 6 }}>
              <div>build: {liveBuildMarker}</div>
              <div>CLEAN REWRITE V4</div>
              <div>todaySeenKpi = {todaySeenKpi}</div>
            </div>
          ) : null}
        </div>

        <div className="kpi-card kpi-orange">
          <div className="kpi-top">
            <span className="kpi-icon"><LogoutOutlined /></span>
            <span className="kpi-title">Təsdiqli çıxış</span>
          </div>
          <div className="kpi-value" data-testid="live-confirmed-checkouts">{confirmedCheckoutsKpi}</div>
          <div className="kpi-trend">↑ exit cihazı/manual</div>
          {showDebug ? (
            <div style={{ color: '#6b7280', fontSize: 11, marginTop: 6 }}>
              <div>build: {liveBuildMarker}</div>
              <div>CLEAN REWRITE V4</div>
              <div>confirmedCheckoutsKpi = {confirmedCheckoutsKpi}</div>
            </div>
          ) : null}
        </div>

        <div className="kpi-card kpi-purple">
          <div className="kpi-top">
            <span className="kpi-icon"><ClockCircleOutlined /></span>
            <span className="kpi-title">Toplam saat</span>
          </div>
          <div className="kpi-value" data-testid="live-total-duration">{totalDurationKpi}</div>
          <div className="kpi-trend">↑ bugünkü işlənmiş vaxt</div>
          {showDebug ? (
            <div style={{ color: '#6b7280', fontSize: 11, marginTop: 6 }}>
              <div>build: {liveBuildMarker}</div>
              <div>CLEAN REWRITE V4</div>
              <div>totalMinutesKpi = {totalMinutesKpi}</div>
              <div>totalDurationKpi = {totalDurationKpi}</div>
            </div>
          ) : null}
        </div>
      </section>

      {showDebug ? (
        <section className="table-card">
          <div className="card-heading">
            <h3>Live debug</h3>
            <Tag color="orange">?debugLive=1</Tag>
          </div>
          <Alert type="info" showIcon message={`Build marker: ${liveBuildMarker}`} />
          <pre style={{ whiteSpace: 'pre-wrap', margin: 0 }}>
            {JSON.stringify({
              buildMarker: liveBuildMarker,
              kpiSource: 'clean-full-file-rewrite-v4',
              requestedSiteId,
              requestedDate,
              tableRowsLength: tableRows.length,
              summaryActiveWorkersCount: summary?.activeWorkersCount,
              summaryTotalWorkersCheckedIn: summary?.totalWorkersCheckedIn,
              summaryTotalWorkedHours: summary?.totalWorkedHours,
              liveStatusActiveWorkersCount: liveStatus?.activeWorkersCount,
              rowTotalMinutes,
              apiTotalMinutes,
              activeWorkersKpi,
              todaySeenKpi,
              confirmedCheckoutsKpi,
              totalMinutesKpi,
              totalDurationKpi,
              firstVisibleSessionWorkerName: tableRows[0]?.workerName,
              firstVisibleSessionCheckInTime: tableRows[0]?.checkInTime,
            }, null, 2)}
          </pre>
        </section>
      ) : null}

      <section className="table-card">
        <div className="card-heading">
          <h2>Bugünkü görünmə sessiyaları</h2>
          <Space>{(summary || liveStatus) && <Tag color="green">Yenilənib: {new Date().toLocaleTimeString()}</Tag>}</Space>
        </div>
        <Table<AttendanceSessionRow>
          columns={columns}
          dataSource={tableRows}
          loading={loading}
          rowKey="id"
          pagination={{ pageSize: 12 }}
          scroll={{ x: 'max-content' }}
          locale={{ emptyText: error ? 'API xətası var. Yuxarıdakı xətanı yoxlayın.' : 'Bu tarix üçün davamiyyət sessiyası tapılmadı. Real kamera tanıma hadisəsi gəldikdən sonra burada davamiyyət görünəcək.' }}
        />
      </section>

      <Modal
        title="Davamiyyət şəkilləri"
        open={galleryOpen}
        onCancel={closeSnapshotGallery}
        footer={null}
        width="80vw"
        centered
      >
        <div className="snapshot-preview-meta">
          <span>İşçi: {galleryWorker?.workerName ?? galleryWorker?.workerExternalId ?? '-'}</span>
          <span>İşçi kodu: {galleryWorker?.workerExternalId ?? '-'}</span>
          <span>Tarix: {requestedDate || '-'}</span>
        </div>
        {galleryLoading ? (
          <Alert type="info" showIcon message="Şəkillər yüklənir..." />
        ) : visibleGallerySnapshots.length ? (
          <div className="snapshot-gallery-grid">
            {visibleGallerySnapshots.map((snapshot) => (
              <figure key={snapshot.id} className="snapshot-gallery-item">
                <AuthenticatedSnapshotImage
                  src={buildTrackBackendApi.attendanceSnapshotUrl(snapshot.snapshotUrl)}
                  alt="Davamiyyət snapshot"
                  placeholder={<span className="snapshot-placeholder">Şəkil yüklənir...</span>}
                />
                <figcaption>
                  <span>{snapshot.eventTimeLocal}</span>
                  <Tag color={snapshot.source.includes('active_register') ? 'purple' : 'cyan'}>{sourceLabel(snapshot.source)}</Tag>
                </figcaption>
              </figure>
            ))}
          </div>
        ) : (
          <Alert type="warning" showIcon message="Bu işçi üçün snapshot tapılmadı" />
        )}
      </Modal>
    </div>
  )
}
