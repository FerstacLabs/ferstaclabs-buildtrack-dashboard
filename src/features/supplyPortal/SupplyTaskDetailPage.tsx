import { ArrowLeftOutlined, CameraOutlined, CheckOutlined, FileDoneOutlined, PlayCircleOutlined, SaveOutlined, UploadOutlined } from '@ant-design/icons'
import { Alert, Button, Card, Descriptions, Form, Image, Input, InputNumber, Progress, Select, Space, Tag, Upload, message } from 'antd'
import type { UploadFile } from 'antd'
import { useEffect, useMemo, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { ProcurementTaskLineStatusTag, ProcurementTaskStatusTag } from '../../components/ui/WarehouseWorkflowStatusTags'
import { buildTrackBackendApi, type ProcurementAttachment, type ProcurementTask, type ProcurementTaskLine, type SupplierRow } from '../../services/api/buildTrackBackendApi'
import { formatCurrency, formatNumber } from '../../utils/formatters'

type ReceiptType = 'Receipt' | 'Invoice'

const dateLabel = (value?: string) => value ? new Date(`${value}T00:00:00`).toLocaleDateString('az-AZ') : '—'
const dateTimeLabel = (value?: string) => value ? new Date(value).toLocaleString('az-AZ') : '—'
const moneyTotal = (line: ProcurementTaskLine) => line.unitPrice ? line.unitPrice * line.purchasedQuantity : 0
const isEditableTask = (status: string) => ['Assigned', 'Accepted', 'Shopping', 'PartiallyCompleted', 'RejectedForCorrection'].includes(status)

const attachmentUrl = (attachment: ProcurementAttachment) => buildTrackBackendApi.supplyAttachmentUrl(attachment.downloadUrl)

export const SupplyTaskDetailPage = () => {
  const { id } = useParams()
  const [task, setTask] = useState<ProcurementTask>()
  const [suppliers, setSuppliers] = useState<SupplierRow[]>([])
  const [loading, setLoading] = useState(false)
  const [uploading, setUploading] = useState(false)
  const [receiptType, setReceiptType] = useState<ReceiptType>('Receipt')
  const [receiptFiles, setReceiptFiles] = useState<UploadFile[]>([])
  const [linePhotoFiles, setLinePhotoFiles] = useState<Record<string, UploadFile[]>>({})

  const supplierOptions = useMemo(() => suppliers.map((supplier) => ({ value: supplier.id, label: supplier.name })), [suppliers])
  const attachments = task?.attachments ?? []
  const receipts = attachments.filter((item) => item.attachmentType === 'Receipt' || item.attachmentType === 'Invoice')
  const totalAmount = useMemo(() => (task?.lines ?? []).reduce((sum, line) => sum + moneyTotal(line), 0), [task])

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
    await load()
  }

  const start = async () => {
    if (!id) return
    setTask(await buildTrackBackendApi.startSupplyTask(id))
    void message.success('Alış prosesi başladı')
    await load()
  }

  const saveLine = async (line: ProcurementTaskLine, values: { purchasedQuantity?: number; unitPrice?: number; supplierId?: string; note?: string }) => {
    if (!id) return
    await buildTrackBackendApi.updateSupplyTaskLinePurchase(id, line.id, {
      purchasedQuantity: values.purchasedQuantity ?? 0,
      unitPrice: values.unitPrice,
      supplierId: values.supplierId,
      note: values.note,
    })
    void message.success('Alış sətri yeniləndi')
    await load()
  }

  const uploadFiles = async (type: ReceiptType | 'ProductPhoto', files: UploadFile[], taskLineId?: string) => {
    if (!id || files.length === 0) return
    setUploading(true)
    try {
      for (const uploadFile of files) {
        const file = uploadFile.originFileObj
        if (!file) continue
        const formData = new FormData()
        formData.append('attachmentType', type)
        if (taskLineId) formData.append('taskLineId', taskLineId)
        formData.append('file', file)
        await buildTrackBackendApi.uploadSupplyTaskAttachment(id, formData)
      }
      void message.success(type === 'ProductPhoto' ? 'Məhsul şəkli yükləndi' : 'Qəbz/faktura yükləndi')
      if (type === 'ProductPhoto' && taskLineId) {
        setLinePhotoFiles((state) => ({ ...state, [taskLineId]: [] }))
      } else {
        setReceiptFiles([])
      }
      await load()
    } finally {
      setUploading(false)
    }
  }

  const submit = async () => {
    if (!id) return
    try {
      setTask(await buildTrackBackendApi.submitSupplyTask(id))
      void message.success('Alış tamamlandı və yoxlamaya göndərildi')
      await load()
    } catch (error) {
      const text = error instanceof Error ? error.message : 'Yoxlamaya göndərmək alınmadı'
      void message.error(text)
    }
  }

  if (!task && loading) return <Card className="soft-card">Tapşırıq yüklənir...</Card>
  if (!task) return <Alert type="warning" showIcon message="Tapşırıq tapılmadı" />

  const editable = isEditableTask(task.status)

  return (
    <div className="field-page supply-page">
      <div className="field-toolbar">
        <div>
          <Link to="/tasks" className="muted-text"><ArrowLeftOutlined /> Tapşırıqlara qayıt</Link>
          <h2>{task.code}</h2>
          <span className="field-eyebrow">Sübutlu satınalma icrası</span>
        </div>
        <Space wrap>
          <ProcurementTaskStatusTag status={task.status} />
          <Button icon={<CheckOutlined />} onClick={accept} disabled={task.status !== 'Assigned'}>Qəbul et</Button>
          <Button icon={<PlayCircleOutlined />} onClick={start} disabled={!['Assigned', 'Accepted', 'RejectedForCorrection'].includes(task.status)}>Alışa başla</Button>
        </Space>
      </div>

      <Alert
        type="info"
        showIcon
        className="field-alert"
        message="Satınalma sübutları tələb olunur"
        description="Hər alınmış material sətri üçün ən azı bir məhsul şəkli və task üzrə ən azı bir qəbz/faktura yüklənməlidir. Management təsdiqi stok artırmır; stok yalnız “Anbara qəbul et” əməliyyatından sonra artır."
      />

      <Card className="soft-card">
        <Descriptions bordered size="small" column={{ xs: 1, md: 2, xl: 4 }}>
          <Descriptions.Item label="Tələb olunan tarix">{dateLabel(task.requiredBy)}</Descriptions.Item>
          <Descriptions.Item label="Təyin tarixi">{dateTimeLabel(task.assignedAt)}</Descriptions.Item>
          <Descriptions.Item label="Agent">{task.assignedProcurementUserName || '—'}</Descriptions.Item>
          <Descriptions.Item label="Toplam alış">{formatCurrency(totalAmount)}</Descriptions.Item>
          <Descriptions.Item label="Rəhbər tapşırığı" span={4}>{task.managerInstruction || '—'}</Descriptions.Item>
        </Descriptions>
      </Card>

      <Space direction="vertical" size={14} className="full-width">
        {task.lines.map((line) => {
          const linePhotos = attachments.filter((item) => item.taskLineId === line.id && item.attachmentType === 'ProductPhoto')
          const progress = line.requestedQuantity > 0 ? Math.min(100, Math.round((line.purchasedQuantity / line.requestedQuantity) * 100)) : 0
          return (
            <Card key={line.id} className="soft-card">
              <div className="card-heading">
                <div>
                  <h2>{line.itemName}</h2>
                  <span className="muted-text">{line.category} / {formatNumber(line.requestedQuantity)} {line.unit}</span>
                </div>
                <ProcurementTaskLineStatusTag status={line.status} />
              </div>
              <Progress percent={progress} status={progress >= 100 ? 'success' : 'active'} />
              <Descriptions size="small" column={{ xs: 1, md: 3 }}>
                <Descriptions.Item label="Alınıb">{formatNumber(line.purchasedQuantity)} {line.unit}</Descriptions.Item>
                <Descriptions.Item label="Qiymət">{line.unitPrice ? formatCurrency(line.unitPrice) : '—'}</Descriptions.Item>
                <Descriptions.Item label="Tədarükçü">{line.supplierName || '—'}</Descriptions.Item>
                <Descriptions.Item label="Qeyd" span={3}>{line.note || '—'}</Descriptions.Item>
              </Descriptions>

              {editable && (
                <Form
                  layout="vertical"
                  className="field-action"
                  initialValues={{ purchasedQuantity: line.purchasedQuantity || line.requestedQuantity, unitPrice: line.unitPrice, supplierId: line.supplierId, note: line.note }}
                  onFinish={(values) => saveLine(line, values)}
                >
                  <div className="field-cart-line">
                    <Form.Item name="purchasedQuantity" label="Alınan miqdar" rules={[{ required: true, message: 'Miqdar yazın' }]}>
                      <InputNumber min={0} max={line.requestedQuantity} className="full-width" />
                    </Form.Item>
                    <Form.Item name="unitPrice" label="Vahid qiymət">
                      <InputNumber min={0} className="full-width" addonAfter="AZN" />
                    </Form.Item>
                    <Form.Item name="supplierId" label="Tədarükçü">
                      <Select allowClear showSearch options={supplierOptions} />
                    </Form.Item>
                    <Form.Item name="note" label="Qeyd">
                      <Input.TextArea rows={2} />
                    </Form.Item>
                    <Button htmlType="submit" icon={<SaveOutlined />}>Yaz</Button>
                  </div>
                </Form>
              )}

              <div className="field-action">
                <Space direction="vertical" size={8} className="full-width">
                  <strong>Məhsul şəkilləri <Tag color={linePhotos.length ? 'green' : 'orange'}>{linePhotos.length}</Tag></strong>
                  {linePhotos.length > 0 && (
                    <Image.PreviewGroup>
                      <Space wrap>
                        {linePhotos.map((photo) => <Image key={photo.id} src={attachmentUrl(photo)} width={86} height={64} style={{ objectFit: 'cover', borderRadius: 6 }} />)}
                      </Space>
                    </Image.PreviewGroup>
                  )}
                  {editable && (
                    <Space wrap>
                      <Upload
                        beforeUpload={() => false}
                        multiple
                        accept="image/*"
                        fileList={linePhotoFiles[line.id] ?? []}
                        onChange={({ fileList }) => setLinePhotoFiles((state) => ({ ...state, [line.id]: fileList }))}
                        {...{ capture: 'environment' }}
                      >
                        <Button icon={<CameraOutlined />}>Şəkil seç</Button>
                      </Upload>
                      <Button loading={uploading} onClick={() => uploadFiles('ProductPhoto', linePhotoFiles[line.id] ?? [], line.id)}>
                        Şəkilləri yüklə
                      </Button>
                    </Space>
                  )}
                </Space>
              </div>
            </Card>
          )
        })}
      </Space>

      <Card className="soft-card">
        <div className="card-heading">
          <h2>Qəbz / faktura</h2>
          <Tag color={receipts.length ? 'green' : 'orange'}>{receipts.length ? `${receipts.length} fayl` : 'Məcburi'}</Tag>
        </div>
        {receipts.length > 0 && (
          <Space wrap className="field-action">
            {receipts.map((receipt) => (
              <Button key={receipt.id} href={attachmentUrl(receipt)} target="_blank" icon={<FileDoneOutlined />}>
                {receipt.originalFileName}
              </Button>
            ))}
          </Space>
        )}
        {editable && (
          <Space wrap className="field-action">
            <Select value={receiptType} onChange={setReceiptType} options={[{ value: 'Receipt', label: 'Qəbz' }, { value: 'Invoice', label: 'Faktura' }]} style={{ width: 140 }} />
            <Upload beforeUpload={() => false} multiple accept="image/*,.pdf,application/pdf" fileList={receiptFiles} onChange={({ fileList }) => setReceiptFiles(fileList)}>
              <Button icon={<UploadOutlined />}>Fayl seç</Button>
            </Upload>
            <Button loading={uploading} onClick={() => uploadFiles(receiptType, receiptFiles)}>Yüklə</Button>
          </Space>
        )}
      </Card>

      <Card className="soft-card">
        <Button type="primary" icon={<CheckOutlined />} onClick={submit} disabled={!editable}>
          Alışı bitir və yoxlamaya göndər
        </Button>
      </Card>
    </div>
  )
}
