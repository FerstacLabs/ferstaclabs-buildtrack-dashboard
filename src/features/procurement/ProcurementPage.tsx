import { CheckCircleOutlined, PlusOutlined, ReloadOutlined, ShoppingCartOutlined } from '@ant-design/icons'
import { Button, Card, Form, Input, Modal, Select, Space, Table, Tabs, Tag, message } from 'antd'
import { useEffect, useMemo, useState } from 'react'
import { KpiCard } from '../../components/ui/KpiCard'
import { PageTitle } from '../../components/ui/PageTitle'
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
  procurementNeedStatusLabel,
  procurementTaskStatusLabel,
  supplierStatusLabel,
  warehouseLineStatusLabel,
  warehouseRequestStatusLabel,
} from '../../utils/warehouseWorkflowLabels'
import { supplyStatusColor } from '../supplyPortal/supplyPortalStore'

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
  const [selectedNeedIds, setSelectedNeedIds] = useState<string[]>([])
  const [agentForm] = Form.useForm()
  const [taskForm] = Form.useForm()

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
    !isTerminalWarehouseRequestStatus(row.status)
    && ['Draft', 'Submitted', 'UnderReview', 'NeedsJustification', 'PendingApproval', 'Approved'].includes(row.status)

  const approveRequest = async (id: string) => {
    await buildTrackBackendApi.approveProcurementWarehouseRequest(id, 'Stok yoxlanıldı və rezerv/procurement axını yaradıldı.')
    void message.success('Sorğu yoxlanıldı')
    await load()
  }

  const issueRequest = async (id: string) => {
    await buildTrackBackendApi.issueProcurementWarehouseRequest(id, { recipientName: 'Prorab', handoverNote: 'Management paneldən verildi' })
    void message.success('Material sahəyə verildi')
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
    await buildTrackBackendApi.verifyProcurementTask(id, 'Sübutlar yoxlandı və təsdiqləndi.')
    void message.success('Tapşırıq təsdiqləndi')
    await load()
  }

  const receiveTask = async (taskId: string) => {
    await buildTrackBackendApi.createGoodsReceipt({ taskId, note: 'Management paneldən anbara qəbul edildi' })
    void message.success('Mal anbara qəbul edildi')
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
                      { title: 'Sətir statusu', render: (_, line) => <Tag color={supplyStatusColor(line.status)}>{warehouseLineStatusLabel(line.status)}</Tag> },
                    ]} />
                  ) }}
                  columns={[
                    { title: 'Kod', dataIndex: 'code' },
                    { title: 'Obyekt', dataIndex: 'siteName' },
                    { title: 'Prorab', dataIndex: 'supervisorName' },
                    { title: 'Təcillik', dataIndex: 'urgency', render: (value) => priorityLabel(value) },
                    { title: 'Status', dataIndex: 'status', render: (_, row) => <Tag key={`${row.id}:${row.status}`} color={row.status === 'ReadyForPickup' ? 'green' : row.status === 'InFulfillment' ? 'orange' : isTerminalWarehouseRequestStatus(row.status) ? 'red' : 'blue'}>{warehouseRequestStatusLabel(row.status)}</Tag> },
                    { title: 'Toplam', render: (_, row) => `${formatNumber(row.totalRequested)} vahid` },
                    { title: 'Çatışmazlıq', render: (_, row) => row.totalShortfall > 0 ? <Tag color="red">{formatNumber(row.totalShortfall)}</Tag> : <Tag color="green">Yoxdur</Tag> },
                    { title: 'Əməliyyat', render: (_, row) => <Space>{canCheckAndReserve(row) && <Button onClick={() => approveRequest(row.id)}>Yoxla/rezerv et</Button>}<Button onClick={() => issueRequest(row.id)} disabled={!['Approved', 'PartiallyApproved', 'ReadyForPickup'].includes(row.status) || row.totalReserved <= 0}>Ver</Button>{isTerminalWarehouseRequestStatus(row.status) && <Tag color="default">Arxiv</Tag>}</Space> },
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
                    { title: 'Status', dataIndex: 'status', render: (value) => <Tag>{procurementNeedStatusLabel(value)}</Tag> },
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
                  { title: 'Status', dataIndex: 'status', render: (value) => <Tag color={supplyStatusColor(value)}>{procurementTaskStatusLabel(value)}</Tag> },
                  { title: 'Sətir', render: (_, row) => row.lines.length },
                  { title: 'Əməliyyat', render: (_, row) => <Space><Button onClick={() => verifyTask(row.id)} disabled={row.status !== 'SubmittedForVerification'}>Təsdiqlə</Button><Button onClick={() => receiveTask(row.id)} disabled={row.status !== 'Verified'}>Anbara qəbul</Button></Space> },
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
    </div>
  )
}
