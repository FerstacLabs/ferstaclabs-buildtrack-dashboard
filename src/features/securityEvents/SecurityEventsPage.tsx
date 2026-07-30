import { EyeInvisibleOutlined, ReloadOutlined, SafetyCertificateOutlined, WarningOutlined } from '@ant-design/icons'
import { Alert, Button, Modal, Select, Space, Table, Tag, message } from 'antd'
import type { TableColumnsType } from 'antd'
import { useEffect, useMemo, useState } from 'react'
import { KpiCard } from '../../components/ui/KpiCard'
import { PageTitle } from '../../components/ui/PageTitle'
import { ToolbarButton } from '../../components/ui/ToolbarButton'
import { buildTrackBackendApi, type BackendSite, type SecurityEventRow, type SecurityEventStatus } from '../../services/api/buildTrackBackendApi'

const API_TEST_SITE_ID = 'c235fd3e-2f5b-4cac-bb1d-92a94dd54b23'
const API_TEST_SITE_NAME = 'API Test Obyekti'

const statusColor: Record<SecurityEventStatus, string> = {
  Open: 'orange',
  Reviewed: 'green',
  Ignored: 'default',
}

const statusLabel: Record<SecurityEventStatus, string> = {
  Open: 'Açıq',
  Reviewed: 'Baxılıb',
  Ignored: 'Yox sayılıb',
}

const eventLabel: Record<string, string> = {
  UnknownFace: 'Tanınmayan üz',
  SuspiciousRecognition: 'Şübhəli tanıma',
  IdentityMismatch: 'Şübhəli tanıma',
  IdentityMappingConflict: 'Şübhəli tanıma',
  ParserUncertainSmartEvent: 'Şübhəli tanıma',
}

const eventColor: Record<string, string> = {
  UnknownFace: 'orange',
  SuspiciousRecognition: 'red',
  IdentityMismatch: 'red',
  IdentityMappingConflict: 'red',
  ParserUncertainSmartEvent: 'purple',
}

const showDebug = import.meta.env.DEV || import.meta.env.VITE_SHOW_ATTENDANCE_DEBUG === 'true'

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

const previousBakuIsoDate = (date = new Date()) => {
  const previous = new Date(date)
  previous.setUTCDate(previous.getUTCDate() - 1)
  return bakuIsoDate(previous)
}

const SnapshotThumbnail = ({ row, onPreview }: { row: SecurityEventRow; onPreview: (row: SecurityEventRow) => void }) => {
  const [failed, setFailed] = useState(false)
  if (!row.snapshotPath || row.snapshotDownloadStatus === 'Failed' || failed) {
    return <span className="snapshot-placeholder">Şəkil yüklənmədi</span>
  }

  return (
    <button type="button" className="snapshot-thumb-button" onClick={() => onPreview(row)} aria-label="Tanınmayan üz şəklini böyüt">
      <img
        src={buildTrackBackendApi.securitySnapshotUrl(row.snapshotUrl)}
        alt="Tanınmayan üz"
        width={84}
        height={54}
        className="snapshot-thumb-image"
        onError={() => setFailed(true)}
      />
      <span className="snapshot-zoom-label">Böyüt</span>
    </button>
  )
}
const resolveInitialSiteId = (siteRows: BackendSite[], currentSiteId?: string) => {
  if (currentSiteId) return currentSiteId
  const apiTestSite = siteRows.find((site) => site.id === API_TEST_SITE_ID) ?? siteRows.find((site) => site.name === API_TEST_SITE_NAME)
  if (apiTestSite) return apiTestSite.id === API_TEST_SITE_ID ? apiTestSite.id : API_TEST_SITE_ID
  return siteRows[0]?.id
}

export const SecurityEventsPage = () => {
  const [sites, setSites] = useState<BackendSite[]>([])
  const [siteId, setSiteId] = useState<string>()
  const [date, setDate] = useState(bakuIsoDate())
  const [rows, setRows] = useState<SecurityEventRow[]>([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')
  const [requestedSiteId, setRequestedSiteId] = useState('')
  const [requestedDate, setRequestedDate] = useState('')
  const [selectedSecurityEvent, setSelectedSecurityEvent] = useState<SecurityEventRow | null>(null)
  const [previewOpen, setPreviewOpen] = useState(false)

  const loadSites = async () => {
    try {
      const siteRows = await buildTrackBackendApi.getSites()
      setSites(siteRows)
      const initialSiteId = resolveInitialSiteId(siteRows, siteId)
      if (initialSiteId && initialSiteId !== siteId) setSiteId(initialSiteId)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Obyekt siyahısı yüklənmədi')
    }
  }

  const loadEvents = async (selectedSiteId = siteId, selectedDate = date) => {
    if (!selectedSiteId) return
    setLoading(true)
    setError('')
    setRequestedSiteId(selectedSiteId)
    setRequestedDate(selectedDate)
    try {
      const securityRows = await buildTrackBackendApi.getSecurityEvents(selectedSiteId, selectedDate)
      if (securityRows.length === 0 && selectedDate === bakuIsoDate()) {
        const fallbackDate = previousBakuIsoDate()
        const fallbackRows = await buildTrackBackendApi.getSecurityEvents(selectedSiteId, fallbackDate)
        if (fallbackRows.length > 0) {
          setDate(fallbackDate)
          setRequestedDate(fallbackDate)
          setRows(fallbackRows)
          return
        }
      }
      setRows(securityRows)
    } catch (err) {
      setRows([])
      setError(err instanceof Error ? err.message : 'Tanınmayan üz hadisələri yüklənmədi')
    } finally {
      setLoading(false)
    }
  }

  const openPreview = (row: SecurityEventRow) => {
    setSelectedSecurityEvent(row)
    setPreviewOpen(true)
  }

  const closePreview = () => {
    setPreviewOpen(false)
    setSelectedSecurityEvent(null)
  }

  const reviewEvent = async (eventId: string, status: SecurityEventStatus) => {
    try {
      await buildTrackBackendApi.reviewSecurityEvent(eventId, { status })
      message.success(status === 'Reviewed' ? 'Hadisə baxıldı kimi qeyd olundu' : 'Hadisə yox sayıldı')
      await loadEvents()
    } catch (err) {
      message.error(err instanceof Error ? err.message : 'Status yenilənmədi')
    }
  }

  useEffect(() => {
    void loadSites()
  }, [])

  useEffect(() => {
    void loadEvents(siteId, date)
  }, [siteId, date])

  const siteOptions = useMemo(() => {
    const options = sites.map((site) => ({ label: site.name, value: site.id }))
    if (!options.some((option) => option.value === API_TEST_SITE_ID)) options.unshift({ label: API_TEST_SITE_NAME, value: API_TEST_SITE_ID })
    return options
  }, [sites])

  const openCount = rows.filter((row) => row.status === 'Open').length
  const reviewedCount = rows.filter((row) => row.status === 'Reviewed').length
  const unknownCount = rows.filter((row) => row.eventType === 'UnknownFace').length
  const suspiciousCount = rows.filter((row) => row.eventType !== 'UnknownFace').length

  const columns: TableColumnsType<SecurityEventRow> = [
    { title: 'Vaxt', dataIndex: 'eventTimeLocal', sorter: (a, b) => a.eventTime.localeCompare(b.eventTime) },
    { title: 'Cihaz', dataIndex: 'deviceName', render: (value) => value ?? '-' },
    { title: 'Hadisə', dataIndex: 'eventType', render: (value) => <Tag color={eventColor[value] ?? 'orange'}>{eventLabel[value] ?? value}</Tag> },
    {
      title: 'Şəkil',
      dataIndex: 'snapshotUrl',
      render: (_, row) => <SnapshotThumbnail row={row} onPreview={openPreview} />,
    },
    { title: 'Status', dataIndex: 'status', render: (value: SecurityEventStatus) => <Tag color={statusColor[value]}>{statusLabel[value]}</Tag> },
    { title: 'RecNo', dataIndex: 'rawRecNo', render: (value) => value ?? '-' },
    { title: 'Qeyd', dataIndex: 'message', render: (value) => value ?? 'Tanınmayan üz aşkarlandı' },
    {
      title: 'Əməliyyat',
      key: 'actions',
      render: (_, row) => (
        <Space>
          <Button size="small" disabled={row.status === 'Reviewed'} onClick={() => reviewEvent(row.id, 'Reviewed')}>Reviewed</Button>
          <Button size="small" disabled={row.status === 'Ignored'} onClick={() => reviewEvent(row.id, 'Ignored')}>Ignore</Button>
        </Space>
      ),
    },
  ]

  return (
    <div className="page-stack">
      <PageTitle title="Tanınmayan üzlər" subtitle="Kamera tanıma sistemi zamanı tanınmayan şəxslər ayrıca təhlükəsizlik hadisəsi kimi saxlanılır" />

      {error && <Alert type="error" showIcon message="Təhlükəsizlik hadisələri yüklənmədi" description={error} />}

      <Alert
        type="warning"
        showIcon
        message="Yad şəxslər və tanınmayan üzlər"
        description="Bu səhifə əməkhaqqı davamiyyətinə təsir etmir. Tanınmayan üzlər və identity mismatch kimi yoxlanılmalı face hadisələri yalnız security_events cədvəlində saxlanılır. Snapshot backend storage-dan göstərilir, kamera admin məlumatları frontend-ə çıxmır."
      />

      <section className="filter-bar live-filter-bar">
        <Select value={siteId} onChange={setSiteId} options={siteOptions} placeholder="Obyekt seçin" />
        <input className="ant-input" type="date" value={date} onChange={(event) => setDate(event.target.value)} style={{ maxWidth: 180 }} />
        <ToolbarButton icon={<ReloadOutlined />} onClick={() => loadEvents()}>Yenilə</ToolbarButton>
      </section>

      {showDebug && (
        <Alert
          type="warning"
          showIcon
          message="Security Events debug"
          description={`requestedSiteId=${requestedSiteId || '-'} | requestedDate=${requestedDate || '-'} | liveStatus.activeWorkersCount=- | liveStatus.workers.length=- | daily.sessions.length=- | securityEvents.length=${rows.length}`}
        />
      )}

      <section className="kpi-grid">
        <KpiCard icon={<EyeInvisibleOutlined />} title="Tanınmayan üzlər" value={unknownCount.toString()} trend={date} tone="orange" />
        <KpiCard icon={<WarningOutlined />} title="Yoxlanılmalı" value={suspiciousCount.toString()} trend="şübhəli tanıma" tone="purple" />
        <KpiCard icon={<WarningOutlined />} title="Açıq hadisə" value={openCount.toString()} trend="baxış gözləyir" tone="red" />
        <KpiCard icon={<SafetyCertificateOutlined />} title="Baxılıb" value={reviewedCount.toString()} trend="security review" tone="green" />
      </section>

      <section className="table-card">
        <div className="card-heading">
          <h2>Yad şəxslər siyahısı</h2>
          <Space>{rows.length > 0 && <Tag color="orange">{rows.length} hadisə</Tag>}</Space>
        </div>
        <Table<SecurityEventRow>
          columns={columns}
          dataSource={rows}
          loading={loading}
          rowKey="id"
          pagination={{ pageSize: 10 }}
          scroll={{ x: 'max-content' }}
          locale={{ emptyText: error ? 'API xətası var. Yuxarıdakı xətanı yoxlayın.' : 'Bu tarix üçün tanınmayan və ya yoxlanılmalı üz hadisəsi tapılmadı.' }}
        />
      </section>
      <Modal
        title="Tanınmayan üz şəkli"
        open={previewOpen}
        onCancel={closePreview}
        footer={null}
        centered
        width={900}
        destroyOnHidden
      >
        {selectedSecurityEvent && (
          <div className="snapshot-preview-modal">
            <div className="snapshot-preview-meta">
              <Tag color="orange">{eventLabel[selectedSecurityEvent.eventType] ?? selectedSecurityEvent.eventType}</Tag>
              <span>Vaxt: {selectedSecurityEvent.eventTimeLocal}</span>
              <span>Cihaz: {selectedSecurityEvent.deviceName ?? '-'}</span>
              <span>RecNo: {selectedSecurityEvent.rawRecNo ?? '-'}</span>
            </div>
            <img
              src={buildTrackBackendApi.securitySnapshotUrl(selectedSecurityEvent.snapshotUrl)}
              alt="Tanınmayan üz böyük görüntü"
              className="snapshot-preview-image"
            />
          </div>
        )}
      </Modal>    </div>
  )
}



