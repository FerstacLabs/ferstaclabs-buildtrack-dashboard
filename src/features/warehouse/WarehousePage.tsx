import {
  CheckCircleOutlined,
  ExclamationCircleOutlined,
  InboxOutlined,
  PlusOutlined,
  SendOutlined,
  ToolOutlined,
  WarningOutlined,
} from '@ant-design/icons'
import { Alert, Button, Form, Input, InputNumber, Modal, Progress, Select, Space, Table, Tag, Timeline, message } from 'antd'
import type { TableColumnsType } from 'antd'
import { useEffect, useMemo, useState } from 'react'
import { ProjectSelect } from '../../components/ProjectSelect'
import { KpiCard } from '../../components/ui/KpiCard'
import { PageTitle } from '../../components/ui/PageTitle'
import { buildTrackBackendApi } from '../../services/api/buildTrackBackendApi'
import { formatCurrency, formatNumber } from '../../utils/formatters'

type WarehouseCategory = 'PPE' | 'Tool' | 'Consumable' | 'Material'
type WarehouseStatus = 'Normal' | 'Low' | 'Critical' | 'OutOfStock'
type RequestStatus = 'Tam təmin olunur' | 'Çatışmazlıq var' | 'Təsdiq gözləyir' | 'Verildi'

interface WarehouseItem {
  id: string
  name: string
  category: WarehouseCategory
  unit: string
  stockQuantity: number
  reservedQuantity: number
  issuedQuantity?: number
  minimumQuantity: number
  unitPrice: number
  location: string
  lastUpdated: string
  supplier: string
}

interface WarehouseRequest {
  id: string
  itemId: string
  itemName: string
  requestedQuantity: number
  availableQuantity: number
  shortageQuantity: number
  foremanName: string
  crewName: string
  siteName: string
  reason: string
  status: RequestStatus
  createdAt: string
}

interface RequestFormValues {
  itemId: string
  requestedQuantity: number
  foremanName: string
  crewName: string
  reason: string
}

const initialItems: WarehouseItem[] = [
  { id: 'helmet', name: 'Kaska', category: 'PPE', unit: 'ədəd', stockQuantity: 86, reservedQuantity: 12, minimumQuantity: 35, unitPrice: 14, location: 'A-01 / PPE rəfi', lastUpdated: '2026-08-06 09:20', supplier: 'SafetyPro' },
  { id: 'glove', name: 'İş əlcəyi', category: 'PPE', unit: 'cüt', stockQuantity: 35, reservedQuantity: 8, minimumQuantity: 60, unitPrice: 3.2, location: 'A-02 / PPE rəfi', lastUpdated: '2026-08-06 09:15', supplier: 'BakuTex' },
  { id: 'vest', name: 'Reflektor jilet', category: 'PPE', unit: 'ədəd', stockQuantity: 48, reservedQuantity: 4, minimumQuantity: 25, unitPrice: 7.5, location: 'A-03 / PPE rəfi', lastUpdated: '2026-08-05 18:40', supplier: 'SafetyPro' },
  { id: 'drill-bit-12', name: 'Sverlo 12 mm', category: 'Consumable', unit: 'ədəd', stockQuantity: 18, reservedQuantity: 6, minimumQuantity: 30, unitPrice: 5.8, location: 'B-04 / alətlər', lastUpdated: '2026-08-06 08:55', supplier: 'ToolMarket' },
  { id: 'drill-bit-8', name: 'Sverlo 8 mm', category: 'Consumable', unit: 'ədəd', stockQuantity: 64, reservedQuantity: 10, minimumQuantity: 40, unitPrice: 3.9, location: 'B-04 / alətlər', lastUpdated: '2026-08-06 08:55', supplier: 'ToolMarket' },
  { id: 'cut-disc', name: 'Kəsici disk', category: 'Consumable', unit: 'ədəd', stockQuantity: 120, reservedQuantity: 20, minimumQuantity: 75, unitPrice: 2.6, location: 'B-02 / sərfiyyat', lastUpdated: '2026-08-05 17:30', supplier: 'ToolMarket' },
  { id: 'welding-electrode', name: 'Qaynaq elektrodu', category: 'Consumable', unit: 'kq', stockQuantity: 42, reservedQuantity: 9, minimumQuantity: 50, unitPrice: 4.4, location: 'C-01 / metal işləri', lastUpdated: '2026-08-05 16:10', supplier: 'MetalLine' },
  { id: 'cement', name: 'Sement M400', category: 'Material', unit: 'kisə', stockQuantity: 210, reservedQuantity: 60, minimumQuantity: 180, unitPrice: 8.7, location: 'D-01 / quru anbar', lastUpdated: '2026-08-06 07:45', supplier: 'Karvan Beton' },
  { id: 'waterproofing', name: 'Hidroizolyasiya rulonu', category: 'Material', unit: 'rulon', stockQuantity: 9, reservedQuantity: 2, minimumQuantity: 15, unitPrice: 36, location: 'D-03 / izolyasiya', lastUpdated: '2026-08-04 15:10', supplier: 'Izotex' },
  { id: 'extension-cable', name: 'Uzatma kabeli 30m', category: 'Tool', unit: 'ədəd', stockQuantity: 14, reservedQuantity: 5, minimumQuantity: 10, unitPrice: 28, location: 'B-01 / elektrik alətləri', lastUpdated: '2026-08-06 10:05', supplier: 'ElektroMax' },
]

const initialRequests: WarehouseRequest[] = [
  {
    id: 'REQ-1008',
    itemId: 'glove',
    itemName: 'İş əlcəyi',
    requestedQuantity: 50,
    availableQuantity: 35,
    shortageQuantity: 15,
    foremanName: 'Rəşad Məmmədov',
    crewName: 'Monolit briqadası',
    siteName: 'GOLD PALACE',
    reason: 'Yeni mərtəbə armatur işlərinə başlayır, bütün briqada üçün əlcək lazımdır.',
    status: 'Çatışmazlıq var',
    createdAt: '2026-08-06 10:35',
  },
  {
    id: 'REQ-1007',
    itemId: 'helmet',
    itemName: 'Kaska',
    requestedQuantity: 20,
    availableQuantity: 86,
    shortageQuantity: 0,
    foremanName: 'Elvin Əliyev',
    crewName: 'Suvaq briqadası',
    siteName: 'GOLD PALACE',
    reason: 'Yeni işçilər üçün PPE dəsti.',
    status: 'Təsdiq gözləyir',
    createdAt: '2026-08-06 09:50',
  },
  {
    id: 'REQ-1006',
    itemId: 'drill-bit-12',
    itemName: 'Sverlo 12 mm',
    requestedQuantity: 12,
    availableQuantity: 18,
    shortageQuantity: 0,
    foremanName: 'Səbuhi Kərimli',
    crewName: 'Material və logistika',
    siteName: 'GOLD PALACE',
    reason: 'Elektrik xəttləri üçün deşik işləri.',
    status: 'Verildi',
    createdAt: '2026-08-05 16:25',
  },
]

const categoryLabel: Record<WarehouseCategory, string> = {
  PPE: 'Əməyin mühafizəsi',
  Tool: 'Alət',
  Consumable: 'Sərfiyyat',
  Material: 'Tikinti materialı',
}

const statusColor: Record<WarehouseStatus | RequestStatus, string> = {
  Normal: 'green',
  Low: 'orange',
  Critical: 'red',
  OutOfStock: 'volcano',
  'Tam təmin olunur': 'green',
  'Çatışmazlıq var': 'red',
  'Təsdiq gözləyir': 'orange',
  Verildi: 'blue',
}

const resolveStockStatus = (item: WarehouseItem): WarehouseStatus => {
  const available = item.stockQuantity - item.reservedQuantity
  if (available <= 0) return 'OutOfStock'
  if (available <= item.minimumQuantity * 0.5) return 'Critical'
  if (available <= item.minimumQuantity) return 'Low'
  return 'Normal'
}

const stockStatusLabel: Record<WarehouseStatus, string> = {
  Normal: 'Normal',
  Low: 'Azalır',
  Critical: 'Kritik',
  OutOfStock: 'Bitib',
}

export const WarehousePage = () => {
  const [items, setItems] = useState(initialItems)
  const [requests, setRequests] = useState(initialRequests)
  const [requestModalOpen, setRequestModalOpen] = useState(false)
  const [form] = Form.useForm<RequestFormValues>()

  useEffect(() => {
    let mounted = true
    const loadBackendWarehouse = async () => {
      try {
        const [stockRows, requestRows] = await Promise.all([
          buildTrackBackendApi.getWarehouseStock(),
          buildTrackBackendApi.getProcurementWarehouseRequests(),
        ])
        if (!mounted) return
        if (stockRows.length) {
          setItems(stockRows.map((row) => ({
            id: row.catalogItemId,
            name: row.itemName,
            category: row.category === 'PPE' ? 'PPE' : row.category === 'Alət' ? 'Tool' : row.category === 'Sərfiyyat' ? 'Consumable' : 'Material',
            unit: row.unit,
            stockQuantity: row.onHandQuantity,
            reservedQuantity: row.reservedQuantity,
            issuedQuantity: row.issuedQuantity,
            minimumQuantity: row.minimumQuantity,
            unitPrice: 0,
            location: row.subcategory ?? 'Mərkəzi anbar',
            lastUpdated: new Date().toLocaleString('az-AZ', { hour12: false }),
            supplier: 'BuildTrack',
          })))
        }
        if (requestRows.length) {
          setRequests(requestRows.map((row) => ({
            id: row.code,
            itemId: row.lines[0]?.catalogItemId ?? row.id,
            itemName: row.lines.map((line) => line.itemName).join(', '),
            requestedQuantity: row.totalRequested,
            availableQuantity: row.totalReserved,
            shortageQuantity: row.totalShortfall,
            foremanName: row.supervisorName ?? 'Prorab',
            crewName: row.siteName ?? 'Obyekt',
            siteName: row.siteName ?? 'Obyekt',
            reason: row.generalNote ?? row.lines[0]?.reason ?? '-',
            status: row.totalShortfall > 0 ? 'Çatışmazlıq var' : row.status === 'Issued' ? 'Verildi' : row.status === 'ReadyForPickup' ? 'Tam təmin olunur' : 'Təsdiq gözləyir',
            createdAt: new Date(row.createdAt).toLocaleString('az-AZ', { hour12: false }),
          })))
        }
      } catch {
        // Demo fallback remains available when backend procurement endpoints are not deployed yet.
      }
    }
    void loadBackendWarehouse()
    return () => {
      mounted = false
    }
  }, [])

  const rows = items.map((item) => {
    const availableQuantity = Math.max(0, item.stockQuantity - item.reservedQuantity)
    const status = resolveStockStatus(item)
    return {
      ...item,
      availableQuantity,
      status,
      stockPercent: Math.round((availableQuantity / Math.max(item.minimumQuantity * 2, availableQuantity, 1)) * 100),
      totalValue: availableQuantity * (item.unitPrice ?? 0),
    }
  })

  const stockRecordRows = rows.filter((row) => row.stockQuantity > 0 || row.reservedQuantity > 0 || (row.issuedQuantity ?? 0) > 0 || row.minimumQuantity > 0)
  const hasReliableValue = rows.some((row) => (row.unitPrice ?? 0) > 0)
  const totalAvailableValue = hasReliableValue ? rows.reduce((sum, row) => sum + row.totalValue, 0) : undefined
  const criticalCount = rows.filter((row) => row.status === 'Critical' || row.status === 'OutOfStock').length
  const pendingRequests = requests.filter((request) => request.status !== 'Verildi').length
  const shortageTotal = requests.reduce((sum, request) => sum + request.shortageQuantity, 0)
  const itemOptions = rows.map((item) => ({ label: `${item.name} - ${formatNumber(item.availableQuantity)} ${item.unit} mövcuddur`, value: item.id }))

  const submitRequest = (values: RequestFormValues) => {
    const item = rows.find((entry) => entry.id === values.itemId)
    if (!item) return

    const shortageQuantity = Math.max(0, values.requestedQuantity - item.availableQuantity)
    const request: WarehouseRequest = {
      id: `REQ-${1009 + requests.length}`,
      itemId: item.id,
      itemName: item.name,
      requestedQuantity: values.requestedQuantity,
      availableQuantity: item.availableQuantity,
      shortageQuantity,
      foremanName: values.foremanName,
      crewName: values.crewName,
      siteName: 'GOLD PALACE',
      reason: values.reason,
      status: shortageQuantity > 0 ? 'Çatışmazlıq var' : 'Təsdiq gözləyir',
      createdAt: new Date().toLocaleString('az-AZ', { hour12: false }),
    }

    setRequests((current) => [request, ...current])
    setRequestModalOpen(false)
    form.resetFields()
    void message.success(shortageQuantity > 0
      ? `${item.name}: anbarda ${formatNumber(item.availableQuantity)} ${item.unit} var, ${formatNumber(shortageQuantity)} ${item.unit} əlavə ehtiyac yaradıldı.`
      : `${item.name} sorğusu təsdiq növbəsinə göndərildi.`)
  }

  const columns: TableColumnsType<(typeof rows)[number]> = [
    {
      title: 'Anbar materialı',
      dataIndex: 'name',
      sorter: (a, b) => a.name.localeCompare(b.name),
      render: (value, row) => (
        <strong>
          {value}
          <br />
          <span className="muted-text">{categoryLabel[row.category]} / {row.location}</span>
        </strong>
      ),
    },
    { title: 'Mövcud', render: (_, row) => <Tag color="blue">{formatNumber(row.stockQuantity)} {row.unit}</Tag>, sorter: (a, b) => a.stockQuantity - b.stockQuantity },
    { title: 'Rezerv', render: (_, row) => `${formatNumber(row.reservedQuantity)} ${row.unit}` },
    { title: 'Verilib', render: (_, row) => `${formatNumber(row.issuedQuantity ?? 0)} ${row.unit}` },
    { title: 'Verilə bilər', render: (_, row) => <strong>{formatNumber(row.availableQuantity)} {row.unit}</strong>, sorter: (a, b) => a.availableQuantity - b.availableQuantity },
    { title: 'Minimum', render: (_, row) => `${formatNumber(row.minimumQuantity)} ${row.unit}` },
    { title: 'Qalıq statusu', dataIndex: 'status', render: (value: WarehouseStatus) => <Tag color={statusColor[value]}>{stockStatusLabel[value]}</Tag> },
    { title: 'Stok səviyyəsi', dataIndex: 'stockPercent', render: (value, row) => <Progress percent={Number(value)} size="small" status={row.status === 'Critical' || row.status === 'OutOfStock' ? 'exception' : 'active'} /> },
    { title: 'Dəyər', dataIndex: 'totalValue', align: 'right', render: (value) => formatCurrency(Number(value)) },
    { title: 'Son yenilənmə', dataIndex: 'lastUpdated' },
  ]

  const requestColumns: TableColumnsType<WarehouseRequest> = [
    { title: 'Sorğu', dataIndex: 'id', render: (value, row) => <strong>{value}<br /><span className="muted-text">{row.createdAt}</span></strong> },
    { title: 'Prorab / Briqada', render: (_, row) => <span>{row.foremanName}<br /><span className="muted-text">{row.crewName}</span></span> },
    { title: 'Material', dataIndex: 'itemName' },
    { title: 'İstənilən', render: (_, row) => formatNumber(row.requestedQuantity) },
    { title: 'Anbarda var', render: (_, row) => formatNumber(row.availableQuantity) },
    { title: 'Əlavə ehtiyac', render: (_, row) => row.shortageQuantity > 0 ? <Tag color="red">{formatNumber(row.shortageQuantity)}</Tag> : <Tag color="green">Yoxdur</Tag> },
    { title: 'Status', dataIndex: 'status', render: (value: RequestStatus) => <Tag color={statusColor[value]}>{value}</Tag> },
    { title: 'Qeyd', dataIndex: 'reason', ellipsis: true },
  ]

  const categoryStats = useMemo(() => {
    return Object.entries(categoryLabel).map(([category, label]) => {
      const categoryRows = rows.filter((row) => row.category === category)
      return {
        category,
        label,
        count: categoryRows.length,
        critical: categoryRows.filter((row) => row.status === 'Critical' || row.status === 'OutOfStock').length,
        outOfStock: categoryRows.filter((row) => row.status === 'OutOfStock').length,
      }
    })
  }, [rows])

  return (
    <div className="page-stack">
      <PageTitle
        title="Anbar"
        subtitle="Tikinti materialları, PPE və alət qalıqları. Gələcəkdə 1C ilə stok sinxronizasiyası üçün hazır panel."
        extra={<Space wrap><ProjectSelect pageKey="warehouse" /><Button type="primary" icon={<PlusOutlined />} onClick={() => setRequestModalOpen(true)}>Prorab sorğusu yarat</Button></Space>}
      />

      <Alert
        type="info"
        showIcon
        message="Anbar sorğu məntiqi"
        description="Prorab yalnız ehtiyacını göndərir. Sistem anbarda mövcud qalıqla müqayisə edir: kifayət edirsə təsdiqə düşür, çatmırsa əlavə ehtiyac qeydiyyata alınır."
      />

      <section className="kpi-grid four">
        <KpiCard icon={<InboxOutlined />} title="Stok növü" value={formatNumber(stockRecordRows.length)} trend="anbar kartı" tone="blue" />
        <KpiCard icon={<WarningOutlined />} title="Kritik qalıq" value={formatNumber(criticalCount)} trend="minimumdan aşağı" tone="red" />
        <KpiCard icon={<SendOutlined />} title="Açıq sorğu" value={formatNumber(pendingRequests)} trend="prorab tələbi" tone="orange" />
        <KpiCard icon={<ToolOutlined />} title="Dəyər məlumatı" value={totalAvailableValue === undefined ? 'Məlumat yoxdur' : formatCurrency(totalAvailableValue)} trend={`${formatNumber(shortageTotal)} əlavə ehtiyac`} tone="purple" />
      </section>

      <section className="content-grid wide-side">
        <div className="table-card">
          <div className="card-heading">
            <h2>Anbar qalıqları</h2>
            <Tag color="purple">1C inteqrasiyası üçün demo struktur</Tag>
          </div>
          <Table rowKey="id" columns={columns} dataSource={rows} pagination={{ pageSize: 8 }} scroll={{ x: 1200 }} />
        </div>

        <aside className="panel-card warehouse-side-panel">
          <h2>Kateqoriya xülasəsi</h2>
          <div className="warehouse-category-list">
            {categoryStats.map((stat) => (
              <div key={stat.category} className="warehouse-category-row">
                <span>{stat.label}</span>
                <strong>{formatNumber(stat.count)} mövqe</strong>
                <Tag color={stat.critical ? 'red' : 'green'}>{stat.critical ? `${stat.critical} kritik / ${stat.outOfStock} bitib` : 'normal'}</Tag>
              </div>
            ))}
          </div>
          <Timeline
            className="warehouse-flow"
            items={[
              { color: 'blue', children: 'Prorab app-dən ehtiyac göndərir' },
              { color: 'green', children: 'Anbar mövcud qalıqla avtomatik müqayisə edir' },
              { color: 'orange', children: 'Çatışmayan miqdar varsa əlavə ehtiyac yaradılır' },
              { color: 'purple', children: 'Təsdiqdən sonra material verilir və 1C çıxış sənədi hazırlanır' },
            ]}
          />
        </aside>
      </section>

      <section className="table-card">
        <div className="card-heading">
          <h2>Prorab sorğuları</h2>
          <Space wrap>
            <Tag color="red">Çatışmazlıq: {formatNumber(shortageTotal)}</Tag>
            <Tag color="blue">Sənəd axını: Demo</Tag>
          </Space>
        </div>
        <Table rowKey="id" columns={requestColumns} dataSource={requests} pagination={{ pageSize: 6 }} scroll={{ x: 1050 }} />
      </section>

      <Modal
        title="Prorab material sorğusu"
        open={requestModalOpen}
        onCancel={() => setRequestModalOpen(false)}
        footer={null}
        width={560}
      >
        <Form
          form={form}
          layout="vertical"
          onFinish={submitRequest}
          initialValues={{ foremanName: 'Rəşad Məmmədov', crewName: 'Monolit briqadası', requestedQuantity: 1 }}
        >
          <Form.Item name="itemId" label="Material / alət" rules={[{ required: true, message: 'Material seçin' }]}>
            <Select showSearch options={itemOptions} placeholder="Anbar materialı seçin" />
          </Form.Item>
          <Form.Item name="requestedQuantity" label="İstənilən miqdar" rules={[{ required: true, message: 'Miqdar daxil edin' }]}>
            <InputNumber min={1} style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item name="foremanName" label="Prorab" rules={[{ required: true }]}>
            <Input />
          </Form.Item>
          <Form.Item name="crewName" label="Briqada" rules={[{ required: true }]}>
            <Input />
          </Form.Item>
          <Form.Item name="reason" label="Qeyd / istifadə səbəbi" rules={[{ required: true, message: 'Sorğu səbəbini yazın' }]}>
            <Input.TextArea rows={3} placeholder="Məsələn: 50 əlcək lazımdır, armatur işləri başlayır..." />
          </Form.Item>
          <Alert
            type="warning"
            showIcon
            icon={<ExclamationCircleOutlined />}
            message="Demo qayda"
            description="Anbarda tam miqdar varsa sorğu təsdiq gözləyir. Çatışmazlıq varsa sistem avtomatik əlavə ehtiyac qeyd edir."
            style={{ marginBottom: 16 }}
          />
          <Button type="primary" icon={<CheckCircleOutlined />} htmlType="submit" block>Sorğunu yoxla və göndər</Button>
        </Form>
      </Modal>
    </div>
  )
}
