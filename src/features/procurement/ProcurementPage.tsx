import { CheckCircleOutlined, PlusOutlined, ReloadOutlined, ShoppingCartOutlined } from '@ant-design/icons'
import { Button, Card, Descriptions, Drawer, Form, Image, Input, Modal, Select, Space, Table, Tabs, Tag, message } from 'antd'
import { useEffect, useMemo, useState } from 'react'
import { KpiCard } from '../../components/ui/KpiCard'
import { PageTitle } from '../../components/ui/PageTitle'
import {
  ProcurementNeedStatusTag,
  ProcurementTaskLineStatusTag,
  ProcurementTaskStatusTag,
  WarehouseLineStatusTag,
  WarehouseRequestStatusTag,
} from '../../components/ui/WarehouseWorkflowStatusTags'
import {
  buildTrackBackendApi,
  type ManagementWarehouseRequest,
  type ProcurementAgent,
  type ProcurementNeed,
  type ProcurementTask,
  type SupplierRow,
  type WarehouseStockItem,
} from '../../services/api/buildTrackBackendApi'
import { formatNumber } from '../../utils/formatters'
import {
  isTerminalWarehouseRequestStatus,
  priorityLabel,
  procurementAgentStatusLabel,
  supplierStatusLabel,
} from '../../utils/warehouseWorkflowLabels'

export const ProcurementPage = () => {
  const [stock, setStock] = useState<WarehouseStockItem[]>([])
  const [requests, setRequests] = useState<ManagementWarehouseRequest[]>([])
  const [needs, setNeeds] = useState<ProcurementNeed[]>([])
  const [tasks, setTasks] = useState<ProcurementTask[]>([])
  const [agents, setAgents] = useState<ProcurementAgent[]>([])
  const [suppliers, setSuppliers] = useState<SupplierRow[]>([])
  const [loading, setLoading] = useState(false)
  const [agentModalOpen, setAgentModalOpen] = useState(false)
  const [taskModalOpen, setTaskModalOpen] = useState(false)
  const [handoverRequest, setHandoverRequest] = useState<ManagementWarehouseRequest | null>(null)
  const [detailTask, setDetailTask] = useState<ProcurementTask | null>(null)
  const [returnModalOpen, setReturnModalOpen] = useState(false)
  const [tableRevision, setTableRevision] = useState(0)
  const [selectedNeedIds, setSelectedNeedIds] = useState<string[]>([])
  const [agentForm] = Form.useForm()
  const [taskForm] = Form.useForm()
  const [handoverForm] = Form.useForm<{ recipientName?: string; handoverNote?: string }>()
  const [returnForm] = Form.useForm<{ note?: string }>()

  const load = async () => {
    setLoading(true)
    try {
      const [nextStock, nextRequests, nextNeeds, nextTasks, nextAgents, nextSuppliers] = await Promise.all([
        buildTrackBackendApi.getWarehouseStock(),
        buildTrackBackendApi.getProcurementWarehouseRequests(),
        buildTrackBackendApi.getProcurementNeeds(),
        buildTrackBackendApi.getProcurementTasks(),
        buildTrackBackendApi.getProcurementAgents(),
        buildTrackBackendApi.getSuppliers(),
      ])
      setStock(nextStock)
      setRequests(nextRequests)
      setNeeds(nextNeeds)
      setTasks(nextTasks)
      setAgents(nextAgents)
      setSuppliers(nextSuppliers)
      setTableRevision((revision) => revision + 1)
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    void load()
  }, [])

  const agentOptions = useMemo(() => agents.map((agent) => ({ value: agent.id, label: agent.fullName })), [agents])
  const activeNeeds = useMemo(() => needs.filter((need) => !['Received', 'Cancelled'].includes(need.status)), [needs])
  const activeTasks = useMemo(() => tasks.filter((task) => !['Completed', 'Verified', 'Cancelled'].includes(task.status)), [tasks])
  const canCheckAndReserve = (row: ManagementWarehouseRequest) =>
    row.status === 'Approved'
    && row.totalReserved === 0
    && row.lines.every((line) => line.reservedQuantity === 0 && line.approvedQuantity === 0)
  const canIssueRequest = (row: ManagementWarehouseRequest) =>
    row.status === 'ReadyForPickup'
    && row.lines.every((line) => {
      const remainingToIssue = Math.max(0, line.requestedQuantity - line.issuedQuantity)
      return remainingToIssue > 0 && line.reservedQuantity >= remainingToIssue && line.shortfallQuantity <= 0
    })

  const approveRequest = async (id: string) => {
    await buildTrackBackendApi.approveProcurementWarehouseRequest(id, 'Köhnə təsdiqli sorğu üçün stok yoxlanıldı və rezerv axını yaradıldı.')
    void message.success('Sorğu yoxlanıldı')
    await load()
  }

  const openHandover = (row: ManagementWarehouseRequest) => {
    handoverForm.setFieldsValue({ recipientName: row.supervisorName ?? 'Prorab', handoverNote: '' })
    setHandoverRequest(row)
  }

  const issueRequest = async (values: { recipientName?: string; handoverNote?: string }) => {
    if (!handoverRequest) return
    await buildTrackBackendApi.issueProcurementWarehouseRequest(handoverRequest.id, {
      recipientName: values.recipientName,
      handoverNote: values.handoverNote || 'Management paneldən təhvil verildi',
    })
    void message.success('Material sahəyə verildi')
    setHandoverRequest(null)
    handoverForm.resetFields()
    await load()
  }

  const createAgent = async (values: { fullName: string; email: string; phone?: string; temporaryPassword: string }) => {
    await buildTrackBackendApi.createProcurementAgent(values)
    void message.success('Təchizatçı agent yaradıldı')
    setAgentModalOpen(false)
    agentForm.resetFields()
    await load()
  }

  const createTask = async (values: { assignedProcurementUserId?: string; managerInstruction?: string }) => {
    const needIds = selectedNeedIds.length ? selectedNeedIds : activeNeeds.filter((need) => need.status === 'PendingApproval' || need.status === 'Approved').map((need) => need.id)
    if (!needIds.length) {
      void message.warning('Tapşırıq üçün ehtiyac seçin')
      return
    }
    await buildTrackBackendApi.createProcurementTask({ needIds, assignedProcurementUserId: values.assignedProcurementUserId, managerInstruction: values.managerInstruction })
    void message.success('Satınalma tapşırığı yaradıldı')
    setTaskModalOpen(false)
    setSelectedNeedIds([])
    taskForm.resetFields()
    await load()
  }

  const verifyTask = async (id: string) => {
    const updated = await buildTrackBackendApi.verifyProcurementTask(id, 'Sübutlar yoxlandı və təsdiqləndi.')
    setDetailTask(updated)
    void message.success('Tapşırıq təsdiqləndi')
    await load()
  }

  const receiveTask = async (taskId: string) => {
    await buildTrackBackendApi.createGoodsReceipt({ taskId, note: 'Management paneldən anbara qəbul edildi' })
    void message.success('Mal anbara qəbul edildi')
    setDetailTask(null)
    await load()
  }

  const openTaskDetails = async (taskId: string) => {
    const task = await buildTrackBackendApi.getProcurementTask(taskId)
    setDetailTask(task)
  }

  const returnForCorrection = async (values: { note?: string }) => {
    if (!detailTask) return
    const updated = await buildTrackBackendApi.returnProcurementTaskForCorrection(detailTask.id, values.note || 'Sübutlar üzrə düzəliş tələb olunur.')
    setDetailTask(updated)
    setReturnModalOpen(false)
    returnForm.resetFields()
    void message.success('Task düzəliş üçün geri qaytarıldı')
    await load()
  }

  return (
    <div className="page-stack">
      <PageTitle
        title="Təchizat / Satınalma"
        subtitle="Sahə material sorğularından anbar rezervasiyası, çatışmazlıq, satınalma tapşırığı, sübut və mal qəbulu axını."
        extra={<Space wrap><Button icon={<ReloadOutlined />} onClick={() => void load()}>Yenilə</Button><Button type="primary" icon={<PlusOutlined />} onClick={() => setTaskModalOpen(true)}>Tapşırıq yarat</Button></Space>}
      />

      <section className="kpi-grid four">
        <KpiCard icon={<ShoppingCartOutlined />} title="Satınalma ehtiyacı" value={formatNumber(activeNeeds.length)} trend="çatışmazlıqdan yaranıb" tone="orange" />
        <KpiCard icon={<CheckCircleOutlined />} title="Hazır sorğu" value={formatNumber(requests.filter((row) => row.status === 'ReadyForPickup').length)} trend="sahəyə verilməlidir" tone="green" />
        <KpiCard icon={<ShoppingCartOutlined />} title="Aktiv tapşırıq" value={formatNumber(activeTasks.length)} trend="təchizat portalında" tone="blue" />
        <KpiCard icon={<PlusOutlined />} title="Tədarükçü" value={formatNumber(suppliers.length)} trend="qiymət tarixçəsi üçün" tone="purple" />
      </section>

      <Tabs
        items={[
          {
            key: 'requests',
            label: 'Sahə sorğuları',
            children: (
              <Card className="soft-card">
                <Table
                  key={`procurement-requests:${tableRevision}`}
                  rowKey="id"
                  loading={loading}
                  dataSource={requests}
                  pagination={{ pageSize: 8 }}
                  expandable={{ expandedRowRender: (row) => (
                    <Table rowKey="id" pagination={false} dataSource={row.lines} columns={[
                      { title: 'Material', dataIndex: 'itemName' },
                      { title: 'İstənilən', render: (_, line) => `${formatNumber(line.requestedQuantity)} ${line.unit}` },
                      { title: 'Anbarda', render: (_, line) => `${formatNumber(line.onHandQuantity)} ${line.unit}` },
                      { title: 'Rezerv', render: (_, line) => `${formatNumber(line.reservedQuantity)} ${line.unit}` },
                      { title: 'Çatışmazlıq', render: (_, line) => line.shortfallQuantity > 0 ? <Tag color="red">{formatNumber(line.shortfallQuantity)} {line.unit}</Tag> : <Tag color="green">Yoxdur</Tag> },
                      { title: 'Sətir statusu', render: (_, line) => <WarehouseLineStatusTag status={line.status} /> },
                    ]} />
                  ) }}
                  columns={[
                    { title: 'Kod', dataIndex: 'code' },
                    { title: 'Obyekt', dataIndex: 'siteName' },
                    { title: 'Prorab', dataIndex: 'supervisorName' },
                    { title: 'Təcillik', dataIndex: 'urgency', render: (value) => priorityLabel(value) },
                    { title: 'Status', render: (_, row) => <WarehouseRequestStatusTag status={row.status} /> },
                    { title: 'Toplam', render: (_, row) => `${formatNumber(row.totalRequested)} vahid` },
                    { title: 'Çatışmazlıq', render: (_, row) => row.totalShortfall > 0 ? <Tag color="red">{formatNumber(row.totalShortfall)}</Tag> : <Tag color="green">Yoxdur</Tag> },
                    { title: 'Əməliyyat', render: (_, row) => <Space>{canCheckAndReserve(row) && <Button onClick={() => approveRequest(row.id)}>Stoku yoxla və rezerv et</Button>}<Button onClick={() => openHandover(row)} disabled={!canIssueRequest(row)}>Təhvil ver</Button>{isTerminalWarehouseRequestStatus(row.status) && <Tag color="default">Arxiv</Tag>}</Space> },
                  ]}
                />
              </Card>
            ),
          },
          {
            key: 'stock',
            label: 'Anbar qalığı',
            children: (
              <Card className="soft-card">
                <Table rowKey="catalogItemId" loading={loading} dataSource={stock} pagination={{ pageSize: 10 }} columns={[
                  { title: 'Material', dataIndex: 'itemName' },
                  { title: 'Kateqoriya', dataIndex: 'category' },
                  { title: 'Mövcud', render: (_, row) => `${formatNumber(row.onHandQuantity)} ${row.unit}` },
                  { title: 'Rezerv', render: (_, row) => `${formatNumber(row.reservedQuantity)} ${row.unit}` },
                  { title: 'Verilə bilər', render: (_, row) => <strong>{formatNumber(row.availableQuantity)} {row.unit}</strong> },
                  { title: 'Status', dataIndex: 'stockStatus', render: (value) => <Tag color={value === 'Normal' ? 'green' : value === 'Bitib' ? 'red' : 'orange'}>{value}</Tag> },
                ]} />
              </Card>
            ),
          },
          {
            key: 'needs',
            label: 'Satınalma ehtiyacları',
            children: (
              <Card className="soft-card">
                <Table
                  rowKey="id"
                  loading={loading}
                  rowSelection={{ selectedRowKeys: selectedNeedIds, onChange: (keys) => setSelectedNeedIds(keys.map(String)) }}
                  dataSource={activeNeeds}
                  pagination={{ pageSize: 8 }}
                  columns={[
                    { title: 'Material', dataIndex: 'itemName' },
                    { title: 'Çatışmazlıq', render: (_, row) => `${formatNumber(row.shortfallQuantity)} ${row.unit}` },
                    { title: 'Prioritet', dataIndex: 'priority', render: (value) => priorityLabel(value) },
                    { title: 'Status', render: (_, row) => <ProcurementNeedStatusTag status={row.status} /> },
                    { title: 'Səbəb', dataIndex: 'reason' },
                  ]}
                />
              </Card>
            ),
          },
          {
            key: 'tasks',
            label: 'Tapşırıqlar',
            children: (
              <Card className="soft-card">
                <Table rowKey="id" loading={loading} dataSource={tasks} pagination={{ pageSize: 8 }} columns={[
                  { title: 'Tapşırıq', dataIndex: 'code' },
                  { title: 'Agent', dataIndex: 'assignedProcurementUserName', render: (value) => value || '-' },
                  { title: 'Status', render: (_, row) => <ProcurementTaskStatusTag status={row.status} /> },
                  { title: 'Tələb olunan tarix', dataIndex: 'requiredBy', render: (value) => value ? new Date(`${value}T00:00:00`).toLocaleDateString('az-AZ') : '—' },
                  { title: 'Sətir', render: (_, row) => row.lines.length },
                  { title: 'Əməliyyat', render: (_, row) => <Space><Button onClick={() => openTaskDetails(row.id)}>Bax</Button><Button onClick={() => verifyTask(row.id)} disabled={row.status !== 'SubmittedForVerification'}>Təsdiqlə</Button><Button onClick={() => receiveTask(row.id)} disabled={row.status !== 'Verified'}>Anbara qəbul et</Button></Space> },
                ]} />
              </Card>
            ),
          },
          {
            key: 'agents',
            label: 'Təchizatçılar',
            children: (
              <Card className="soft-card">
                <Button className="field-action" type="primary" onClick={() => setAgentModalOpen(true)}>Satınalma əməkdaşı yarat</Button>
                <Table rowKey="id" dataSource={agents} pagination={{ pageSize: 8 }} columns={[
                  { title: 'Ad', dataIndex: 'fullName' },
                  { title: 'Email', dataIndex: 'email' },
                  { title: 'Açıq tapşırıq', dataIndex: 'openTasks' },
                  { title: 'Status', dataIndex: 'status', render: (value) => <Tag color={value === 'Active' ? 'green' : 'red'}>{procurementAgentStatusLabel(value)}</Tag> },
                ]} />
              </Card>
            ),
          },
          {
            key: 'suppliers',
            label: 'Tədarükçü bazası',
            children: (
              <Card className="soft-card">
                <Table rowKey="id" dataSource={suppliers} pagination={{ pageSize: 8 }} columns={[
                  { title: 'Tədarükçü', dataIndex: 'name' },
                  { title: 'Kateqoriya', dataIndex: 'categories' },
                  { title: 'Telefon', dataIndex: 'phone' },
                  { title: 'Status', dataIndex: 'status', render: (value) => <Tag color={value === 'Active' ? 'green' : 'orange'}>{supplierStatusLabel(value)}</Tag> },
                ]} />
              </Card>
            ),
          },
        ]}
      />

      <Modal title="Satınalma əməkdaşı yarat" open={agentModalOpen} onCancel={() => setAgentModalOpen(false)} footer={null}>
        <Form form={agentForm} layout="vertical" onFinish={createAgent}>
          <Form.Item name="fullName" label="Ad soyad" rules={[{ required: true }]}>
            <Input />
          </Form.Item>
          <Form.Item name="email" label="Email" rules={[{ required: true }]}>
            <Input />
          </Form.Item>
          <Form.Item name="phone" label="Telefon">
            <Input />
          </Form.Item>
          <Form.Item name="temporaryPassword" label="Müvəqqəti şifrə" rules={[{ required: true, min: 8 }]}>
            <Input.Password />
          </Form.Item>
          <Button type="primary" htmlType="submit">Yarat</Button>
        </Form>
      </Modal>

      <Modal title="Satınalma tapşırığı yarat" open={taskModalOpen} onCancel={() => setTaskModalOpen(false)} footer={null}>
        <Form form={taskForm} layout="vertical" onFinish={createTask}>
          <Form.Item name="assignedProcurementUserId" label="Təyin ediləcək satınalma əməkdaşı">
            <Select allowClear options={agentOptions} />
          </Form.Item>
          <Form.Item name="managerInstruction" label="Rəhbər tapşırığı">
            <Input.TextArea rows={3} />
          </Form.Item>
          <Button type="primary" htmlType="submit">Tapşırıq yarat</Button>
        </Form>
      </Modal>

      <Drawer
        title={detailTask ? `Satınalma taskı: ${detailTask.code}` : 'Satınalma taskı'}
        open={Boolean(detailTask)}
        width={920}
        onClose={() => setDetailTask(null)}
      >
        {detailTask && (
          <Space direction="vertical" size="middle" className="full-width">
            <Descriptions bordered size="small" column={{ xs: 1, md: 2 }}>
              <Descriptions.Item label="Status"><ProcurementTaskStatusTag status={detailTask.status} /></Descriptions.Item>
              <Descriptions.Item label="Prioritet">{priorityLabel(detailTask.priority)}</Descriptions.Item>
              <Descriptions.Item label="Tələb olunan tarix">{detailTask.requiredBy ? new Date(`${detailTask.requiredBy}T00:00:00`).toLocaleDateString('az-AZ') : '—'}</Descriptions.Item>
              <Descriptions.Item label="Təchizatçı">{detailTask.assignedProcurementUserName || '—'}</Descriptions.Item>
              <Descriptions.Item label="Başlayıb">{detailTask.startedAt ? new Date(detailTask.startedAt).toLocaleString('az-AZ') : '—'}</Descriptions.Item>
              <Descriptions.Item label="Yoxlamaya göndərilib">{detailTask.submittedAt ? new Date(detailTask.submittedAt).toLocaleString('az-AZ') : '—'}</Descriptions.Item>
              <Descriptions.Item label="Rəhbər tapşırığı" span={2}>{detailTask.managerInstruction || '—'}</Descriptions.Item>
              <Descriptions.Item label="Yoxlama qeydi" span={2}>{detailTask.verificationNote || '—'}</Descriptions.Item>
            </Descriptions>

            <Card className="soft-card" size="small">
              <Table
                rowKey="id"
                pagination={false}
                dataSource={detailTask.lines}
                expandable={{
                  expandedRowRender: (line) => {
                    const photos = (detailTask.attachments ?? []).filter((item) => item.taskLineId === line.id && item.attachmentType === 'ProductPhoto')
                    return photos.length ? (
                      <Image.PreviewGroup>
                        <Space wrap>
                          {photos.map((photo) => <Image key={photo.id} src={buildTrackBackendApi.supplyAttachmentUrl(photo.downloadUrl)} width={96} height={72} style={{ objectFit: 'cover', borderRadius: 6 }} />)}
                        </Space>
                      </Image.PreviewGroup>
                    ) : <span className="muted-text">Məhsul şəkli yoxdur</span>
                  },
                }}
                columns={[
                  { title: 'Material', dataIndex: 'itemName' },
                  { title: 'İstənilən', render: (_, line) => `${formatNumber(line.requestedQuantity)} ${line.unit}` },
                  { title: 'Alınıb', render: (_, line) => `${formatNumber(line.purchasedQuantity)} ${line.unit}` },
                  { title: 'Vahid qiymət', render: (_, line) => line.unitPrice ? formatNumber(line.unitPrice) : '—' },
                  { title: 'Toplam', render: (_, line) => line.unitPrice ? `${formatNumber(line.unitPrice * line.purchasedQuantity)} AZN` : '—' },
                  { title: 'Tədarükçü', dataIndex: 'supplierName', render: (value) => value || '—' },
                  { title: 'Status', render: (_, line) => <ProcurementTaskLineStatusTag status={line.status} /> },
                ]}
              />
            </Card>

            <Card className="soft-card" size="small">
              <div className="card-heading">
                <h2>Qəbz / faktura</h2>
                <Tag color={(detailTask.attachments ?? []).some((item) => item.attachmentType === 'Receipt' || item.attachmentType === 'Invoice') ? 'green' : 'orange'}>Task səviyyəsi</Tag>
              </div>
              <Space wrap>
                {(detailTask.attachments ?? []).filter((item) => item.attachmentType === 'Receipt' || item.attachmentType === 'Invoice').map((item) => (
                  <Button key={item.id} href={buildTrackBackendApi.supplyAttachmentUrl(item.downloadUrl)} target="_blank">
                    {item.originalFileName}
                  </Button>
                ))}
                {!(detailTask.attachments ?? []).some((item) => item.attachmentType === 'Receipt' || item.attachmentType === 'Invoice') && <span className="muted-text">Qəbz/faktura yüklənməyib</span>}
              </Space>
            </Card>

            <Space wrap>
              <Button type="primary" onClick={() => verifyTask(detailTask.id)} disabled={detailTask.status !== 'SubmittedForVerification'}>Təsdiqlə</Button>
              <Button danger onClick={() => setReturnModalOpen(true)} disabled={detailTask.status !== 'SubmittedForVerification'}>Geri qaytar</Button>
              <Button onClick={() => receiveTask(detailTask.id)} disabled={detailTask.status !== 'Verified'}>Anbara qəbul et</Button>
            </Space>
          </Space>
        )}
      </Drawer>

      <Modal title="Düzəliş üçün geri qaytar" open={returnModalOpen} onCancel={() => setReturnModalOpen(false)} footer={null} destroyOnHidden>
        <Form form={returnForm} layout="vertical" onFinish={returnForCorrection}>
          <Form.Item name="note" label="Qeyd" rules={[{ required: true, message: 'Düzəliş səbəbini yazın' }]}>
            <Input.TextArea rows={3} placeholder="Məsələn: 2-ci material üçün məhsul şəkli daha aydın yüklənməlidir." />
          </Form.Item>
          <Button type="primary" htmlType="submit">Geri qaytar</Button>
        </Form>
      </Modal>

      <Modal
        title="Materialları təhvil ver"
        open={Boolean(handoverRequest)}
        onCancel={() => setHandoverRequest(null)}
        footer={null}
        width={720}
        destroyOnHidden
      >
        {handoverRequest && (
          <Space direction="vertical" size="middle" className="full-width">
            <div>
              <strong>{handoverRequest.code}</strong>
              <div className="muted-text">{handoverRequest.siteName || 'Obyekt'} / {handoverRequest.supervisorName || 'Prorab'}</div>
            </div>
            <Table
              size="small"
              rowKey="id"
              pagination={false}
              dataSource={handoverRequest.lines}
              columns={[
                { title: 'Material', dataIndex: 'itemName' },
                { title: 'İstənilən', render: (_, line) => `${formatNumber(line.requestedQuantity)} ${line.unit}` },
                { title: 'Rezerv', render: (_, line) => `${formatNumber(line.reservedQuantity)} ${line.unit}` },
                { title: 'Verilib', render: (_, line) => `${formatNumber(line.issuedQuantity)} ${line.unit}` },
                { title: 'Qalan', render: (_, line) => `${formatNumber(Math.max(0, line.requestedQuantity - line.issuedQuantity))} ${line.unit}` },
                { title: 'Status', render: (_, line) => <WarehouseLineStatusTag status={line.status} /> },
              ]}
            />
            <Form form={handoverForm} layout="vertical" onFinish={issueRequest}>
              <Form.Item name="recipientName" label="Təhvil alan">
                <Input placeholder="Prorab və ya məsul şəxs" />
              </Form.Item>
              <Form.Item name="handoverNote" label="Təhvil qeydi">
                <Input.TextArea rows={3} placeholder="Məsələn: materiallar sahəyə təhvil verildi." />
              </Form.Item>
              <Space>
                <Button onClick={() => setHandoverRequest(null)}>İmtina</Button>
                <Button type="primary" htmlType="submit">Təhvil ver</Button>
              </Space>
            </Form>
          </Space>
        )}
      </Modal>
    </div>
  )
}
