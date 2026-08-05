import { EyeInvisibleOutlined, ReloadOutlined, SafetyCertificateOutlined, WarningOutlined } from '@ant-design/icons'
import { Alert, Button, Modal, Select, Space, Table, Tag, message } from 'antd'
import type { TableColumnsType } from 'antd'
import { useEffect, useMemo, useState } from 'react'
import { KpiCard } from '../../components/ui/KpiCard'
import { PageTitle } from '../../components/ui/PageTitle'
import { ToolbarButton } from '../../components/ui/ToolbarButton'
import { AuthenticatedSnapshotImage } from '../../components/ui/AuthenticatedSnapshotImage'
import { buildTrackBackendApi, type BackendSite, type BackendWorker, type SecurityEventRow, type SecurityEventStatus } from '../../services/api/buildTrackBackendApi'

const statusColor: Record<SecurityEventStatus, string> = {
  Open: 'orange',
  PendingCorrelation: 'geekblue',
  Reviewed: 'green',
  Ignored: 'default',
  AutoResolved: 'blue',
}

const statusLabel: Record<SecurityEventStatus, string> = {
  Open: 'Açıq',
  PendingCorrelation: 'Korrelyasiya gözləyir',
  Reviewed: 'Baxılıb',
  Ignored: 'Yox sayılıb',
  AutoResolved: 'Avtomatik bağlanıb',
}

const eventLabel: Record<string, string> = {
  UnknownFace: 'Tanınmayan üz',
  SuspiciousRecognition: 'Şübhəli tanıma',
  IdentityMismatch: 'Şübhəli tanıma',
  IdentityMappingConflict: 'Şübhəli tanıma',
  ParserUncertainSmartEvent: 'Şübhəli tanıma',
  UnmappedCameraIdentity: 'İşçiyə bağlanmayıb',
}

const eventColor: Record<string, string> = {
  UnknownFace: 'orange',
  SuspiciousRecognition: 'red',
  IdentityMismatch: 'red',
  IdentityMappingConflict: 'red',
  ParserUncertainSmartEvent: 'purple',
  UnmappedCameraIdentity: 'blue',
}

const showDebug = import.meta.env.DEV || import.meta.env.VITE_SHOW_ATTENDANCE_DEBUG === 'true'
const statusFilterOptions: Array<{ label: string; value: SecurityEventStatus | 'All' }> = [
  { label: 'Açıq', value: 'Open' },
  { label: 'Baxılıb', value: 'Reviewed' },
  { label: 'Yox sayılıb', value: 'Ignored' },
  { label: 'Avtomatik bağlanıb', value: 'AutoResolved' },
  { label: 'Hamısı', value: 'All' },
]

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
  if (!row.snapshotUrl || row.snapshotDownloadStatus === 'Failed' || failed) {
    return <span className="snapshot-placeholder">Şəkil yüklənmədi</span>
  }

  return (
    <button type="button" className="snapshot-thumb-button" onClick={() => onPreview(row)} aria-label="Tanınmayan üz şəklini böyüt">
      <AuthenticatedSnapshotImage
        src={buildTrackBackendApi.securitySnapshotUrl(row.snapshotUrl)}
        alt="Tanınmayan üz"
        width={84}
        height={54}
        className="snapshot-thumb-image"
        placeholder={<span className="snapshot-placeholder">Şəkil yüklənir...</span>}
        onUnavailable={() => setFailed(true)}
      />
      <span className="snapshot-zoom-label">Böyüt</span>
    </button>
  )
}
const resolveInitialSiteId = (siteRows: BackendSite[], currentSiteId?: string) => {
  if (currentSiteId && siteRows.some((site) => site.id === currentSiteId)) return currentSiteId
  return siteRows[0]?.id
}

export const SecurityEventsPage = () => {
  const [sites, setSites] = useState<BackendSite[]>([])
  const [siteId, setSiteId] = useState<string>()
  const [date, setDate] = useState(bakuIsoDate())
  const [rows, setRows] = useState<SecurityEventRow[]>([])
  const [statusFilter, setStatusFilter] = useState<SecurityEventStatus | 'All'>('Open')
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')
  const [requestedSiteId, setRequestedSiteId] = useState('')
  const [requestedDate, setRequestedDate] = useState('')
  const [selectedSecurityEvent, setSelectedSecurityEvent] = useState<SecurityEventRow | null>(null)
  const [previewOpen, setPreviewOpen] = useState(false)
  const [workers, setWorkers] = useState<BackendWorker[]>([])
  const [linkOpen, setLinkOpen] = useState(false)
  const [linkingEvent, setLinkingEvent] = useState<SecurityEventRow | null>(null)
  const [selectedWorkerId, setSelectedWorkerId] = useState<string>()

  const loadSites = async () => {
    try {
      const siteRows = await buildTrackBackendApi.getSites()
      setSites(siteRows)
      buildTrackBackendApi.getWorkers().then(setWorkers).catch(() => setWorkers([]))
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

  const openLinkWorker = (row: SecurityEventRow) => {
    setLinkingEvent(row)
    setSelectedWorkerId(undefined)
    setLinkOpen(true)
  }

  const linkWorker = async () => {
    if (!linkingEvent || !selectedWorkerId) return
    try {
      await buildTrackBackendApi.linkSecurityEventToWorker(linkingEvent.id, {
        workerId: selectedWorkerId,
        remapRecent: true,
        reviewNote: 'Security hadisəsindən işçi-camera mapping yaradıldı',
      })
      message.success('Kamera hadisəsi işçiyə bağlandı')
      setLinkOpen(false)
      setLinkingEvent(null)
      await loadEvents()
    } catch (err) {
      message.error(err instanceof Error ? err.message : 'İşçiyə bağlama alınmadı')
    }
  }

  useEffect(() => {
    void loadSites()
  }, [])

  useEffect(() => {
    void loadEvents(siteId, date)
  }, [siteId, date])

  const siteOptions = useMemo(() => {
    return sites.map((site) => ({ label: site.name, value: site.id }))
  }, [sites])

  const openRows = rows.filter((row) => row.status === 'Open')
  const filteredRows = statusFilter === 'All' ? rows : rows.filter((row) => row.status === statusFilter)
  const openCount = openRows.length
  const reviewedCount = rows.filter((row) => row.status === 'Reviewed').length
  const autoResolvedCount = rows.filter((row) => row.status === 'AutoResolved').length
  const unknownCount = openRows.filter((row) => row.eventType === 'UnknownFace').length
  const suspiciousCount = openRows.filter((row) => row.eventType !== 'UnknownFace').length

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
          <Button size="small" disabled={row.status !== 'Open'} onClick={() => reviewEvent(row.id, 'Reviewed')}>Baxıldı</Button>
          <Button size="small" disabled={row.status !== 'Open'} onClick={() => reviewEvent(row.id, 'Ignored')}>Yox say</Button>
          {(row.cameraCardName || row.cameraExternalUserId || row.eventType === 'UnmappedCameraIdentity') && (
            <Button size="small" disabled={row.status !== 'Open'} onClick={() => openLinkWorker(row)}>İşçiyə bağla</Button>
          )}
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
        <Select value={statusFilter} onChange={setStatusFilter} options={statusFilterOptions} style={{ minWidth: 180 }} />
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
        <KpiCard icon={<SafetyCertificateOutlined />} title="Bağlanıb" value={(reviewedCount + autoResolvedCount).toString()} trend={`${autoResolvedCount} avtomatik`} tone="green" />
      </section>

      <section className="table-card">
        <div className="card-heading">
          <h2>Yad şəxslər siyahısı</h2>
          <Space>{filteredRows.length > 0 && <Tag color="orange">{filteredRows.length} hadisə</Tag>}</Space>
        </div>
        <Table<SecurityEventRow>
          columns={columns}
          dataSource={filteredRows}
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
        width="80vw"
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
            {selectedSecurityEvent.snapshotUrl ? (
              <AuthenticatedSnapshotImage
                src={buildTrackBackendApi.securitySnapshotUrl(selectedSecurityEvent.snapshotUrl)}
                alt="Tanınmayan üz böyük görüntü"
                className="snapshot-preview-image"
                placeholder={<Alert type="info" showIcon message="Şəkil yüklənir..." />}
              />
            ) : (
              <Alert type="warning" showIcon message="Şəkil yüklənmədi" />
            )}
          </div>
        )}
      </Modal>
      <Modal
        title="Kamera hadisəsini işçiyə bağla"
        open={linkOpen}
        onCancel={() => setLinkOpen(false)}
        onOk={linkWorker}
        okText="Bağla"
        cancelText="İmtina"
        okButtonProps={{ disabled: !selectedWorkerId }}
      >
        <Space direction="vertical" style={{ width: '100%' }}>
          <Alert
            type="info"
            showIcon
            message={`Dahua CardName: ${linkingEvent?.cameraCardName || '-'} | UserID: ${linkingEvent?.cameraExternalUserId || '-'}`}
            description="Seçilən işçi üçün worker-camera identity yaradılacaq və uyğun keçmiş kamera qeydləri həmin işçiyə bağlanacaq."
          />
          <Select
            showSearch
            placeholder="İşçi seçin"
            value={selectedWorkerId}
            onChange={setSelectedWorkerId}
            style={{ width: '100%' }}
            options={workers.map((worker) => ({ value: worker.id, label: `${worker.fullName} / ${worker.externalWorkerCode}` }))}
          />
        </Space>
      </Modal>
    </div>
  )
}



