import { ArrowLeftOutlined, CameraOutlined, CheckOutlined, PlayCircleOutlined, SaveOutlined, UploadOutlined } from '@ant-design/icons'
import { Alert, Button, Card, Form, Input, InputNumber, Select, Space, Table, Tag, Upload, message } from 'antd'
import type { UploadFile } from 'antd'
import { useEffect, useMemo, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { buildTrackBackendApi, type ProcurementTask, type SupplierRow } from '../../services/api/buildTrackBackendApi'
import { formatCurrency, formatNumber } from '../../utils/formatters'
import { procurementTaskLineStatusLabel } from '../../utils/warehouseWorkflowLabels'
import { supplyStatusColor, supplyStatusLabel } from './supplyPortalStore'

export const SupplyTaskDetailPage = () => {
  const { id } = useParams()
  const [task, setTask] = useState<ProcurementTask>()
  const [suppliers, setSuppliers] = useState<SupplierRow[]>([])
  const [loading, setLoading] = useState(false)
  const [uploading, setUploading] = useState(false)
  const [receiptFiles, setReceiptFiles] = useState<UploadFile[]>([])
  const [photoFiles, setPhotoFiles] = useState<UploadFile[]>([])

  const supplierOptions = useMemo(() => suppliers.map((supplier) => ({ value: supplier.id, label: supplier.name })), [suppliers])

  const load = async () => {
    if (!id) return
    setLoading(true)
    try {
      const [nextTask, nextSuppliers] = await Promise.all([
        buildTrackBackendApi.getSupplyTask(id),
        buildTrackBackendApi.getSuppliers(),
      ])
      setTask(nextTask)
      setSuppliers(nextSuppliers)
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    void load()
  }, [id])

  const accept = async () => {
    if (!id) return
    setTask(await buildTrackBackendApi.acceptSupplyTask(id))
    void message.success('Tapşırıq qəbul edildi')
  }

  const start = async () => {
    if (!id) return
    setTask(await buildTrackBackendApi.startSupplyTask(id))
    void message.success('Alış prosesi başladı')
  }

  const saveLine = async (lineId: string, values: { purchasedQuantity?: number; unitPrice?: number; supplierId?: string; note?: string }) => {
    if (!id) return
    await buildTrackBackendApi.updateSupplyTaskLinePurchase(id, lineId, {
      purchasedQuantity: values.purchasedQuantity ?? 0,
      unitPrice: values.unitPrice,
      supplierId: values.supplierId,
      note: values.note,
    })
    void message.success('Alış sətri yeniləndi')
    await load()
  }

  const uploadEvidence = async (type: 'Receipt' | 'ProductPhoto', files: UploadFile[]) => {
    if (!id || files.length === 0) return
    const file = files[0].originFileObj
    if (!file) return
    setUploading(true)
    try {
      const formData = new FormData()
      formData.append('attachmentType', type)
      formData.append('file', file)
      await buildTrackBackendApi.uploadSupplyTaskAttachment(id, formData)
      void message.success(type === 'Receipt' ? 'Çek yükləndi' : 'Məhsul şəkli yükləndi')
    } finally {
      setUploading(false)
    }
  }

  const submit = async () => {
    if (!id) return
    setTask(await buildTrackBackendApi.submitSupplyTask(id))
    void message.success('Tapşırıq rəhbər təsdiqinə göndərildi')
  }

  if (!task && loading) return <Card className="soft-card">Tapşırıq yüklənir...</Card>
  if (!task) return <Alert type="warning" showIcon message="Tapşırıq tapılmadı" />

  return (
    <div className="field-page supply-page">
      <div className="field-toolbar">
        <div>
          <Link to="/tasks" className="muted-text"><ArrowLeftOutlined /> Tapşırıqlara qayıt</Link>
          <h2>{task.code}</h2>
          <span className="field-eyebrow">Sübutlu satınalma icrası</span>
        </div>
        <Space wrap>
          <Tag color={supplyStatusColor(task.status)}>{supplyStatusLabel(task.status)}</Tag>
          <Button icon={<CheckOutlined />} onClick={accept} disabled={!['Assigned'].includes(task.status)}>Qəbul et</Button>
          <Button icon={<PlayCircleOutlined />} onClick={start} disabled={!['Assigned', 'Accepted'].includes(task.status)}>Alışa başla</Button>
        </Space>
      </div>

      <Alert
        type="info"
        showIcon
        className="field-alert"
        message="Satınalma sübutları tələb olunur"
        description="Tapşırıq təsdiqə göndərilməzdən əvvəl məhsul şəkli və çek yüklənməlidir. Qiymət məlumatı yalnız supply/management tərəfdə görünür."
      />

      <Card className="soft-card">
        <Table
          rowKey="id"
          loading={loading}
          dataSource={task.lines}
          pagination={false}
          columns={[
            { title: 'Material', render: (_, row) => <strong>{row.itemName}<br /><span className="muted-text">{row.category}</span></strong> },
            { title: 'Lazım olan', render: (_, row) => `${formatNumber(row.requestedQuantity)} ${row.unit}` },
            { title: 'Alınıb', render: (_, row) => `${formatNumber(row.purchasedQuantity)} ${row.unit}` },
            { title: 'Məbləğ', render: (_, row) => row.unitPrice ? formatCurrency(row.unitPrice * row.purchasedQuantity) : '-' },
            { title: 'Status', dataIndex: 'status', render: (value) => <Tag color={supplyStatusColor(value)}>{procurementTaskLineStatusLabel(value)}</Tag> },
            { title: 'Alış yenilə', width: 420, render: (_, row) => (
              <Form
                layout="inline"
                initialValues={{ purchasedQuantity: row.purchasedQuantity || row.requestedQuantity, unitPrice: row.unitPrice, supplierId: row.supplierId, note: row.note }}
                onFinish={(values) => saveLine(row.id, values)}
              >
                <Form.Item name="purchasedQuantity">
                  <InputNumber min={0} placeholder="Miqdar" />
                </Form.Item>
                <Form.Item name="unitPrice">
                  <InputNumber min={0} placeholder="Qiymət" />
                </Form.Item>
                <Form.Item name="supplierId">
                  <Select allowClear showSearch placeholder="Tədarükçü" options={supplierOptions} style={{ width: 140 }} />
                </Form.Item>
                <Form.Item name="note">
                  <Input placeholder="Qeyd" style={{ width: 120 }} />
                </Form.Item>
                <Button htmlType="submit" icon={<SaveOutlined />}>Yaz</Button>
              </Form>
            ) },
          ]}
        />
      </Card>

      <section className="content-grid two">
        <Card className="soft-card">
          <div className="card-heading">
            <h2>Çek yüklə</h2>
            <Tag color="orange">Məcburi</Tag>
          </div>
          <Upload beforeUpload={() => false} maxCount={1} fileList={receiptFiles} onChange={({ fileList }) => setReceiptFiles(fileList)}>
            <Button icon={<UploadOutlined />}>Çek seç</Button>
          </Upload>
          <Button className="field-action" loading={uploading} onClick={() => uploadEvidence('Receipt', receiptFiles)}>Çeki göndər</Button>
        </Card>
        <Card className="soft-card">
          <div className="card-heading">
            <h2>Məhsul şəkli</h2>
            <Tag color="orange">Məcburi</Tag>
          </div>
          <Upload beforeUpload={() => false} maxCount={1} fileList={photoFiles} onChange={({ fileList }) => setPhotoFiles(fileList)}>
            <Button icon={<CameraOutlined />}>Şəkil seç</Button>
          </Upload>
          <Button className="field-action" loading={uploading} onClick={() => uploadEvidence('ProductPhoto', photoFiles)}>Şəkli göndər</Button>
        </Card>
      </section>

      <Card className="soft-card">
        <Button type="primary" icon={<CheckOutlined />} onClick={submit} disabled={!['Shopping', 'PartiallyCompleted', 'Completed'].includes(task.status)}>
          Rəhbər təsdiqinə göndər
        </Button>
      </Card>
    </div>
  )
}
