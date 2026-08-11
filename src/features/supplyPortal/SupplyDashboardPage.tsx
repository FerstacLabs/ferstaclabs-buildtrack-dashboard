import { ClockCircleOutlined, InboxOutlined, ShoppingCartOutlined, UploadOutlined } from '@ant-design/icons'
import { Card, Table, Tag } from 'antd'
import { Link } from 'react-router-dom'
import { KpiCard } from '../../components/ui/KpiCard'
import { formatNumber } from '../../utils/formatters'
import { priorityLabel } from '../../utils/warehouseWorkflowLabels'
import { supplyStatusColor, supplyStatusLabel, useSupplyPortalStore } from './supplyPortalStore'

export const SupplyDashboardPage = () => {
  const { dashboard, tasks, loading } = useSupplyPortalStore()
  const rows = dashboard?.recentTasks.length ? dashboard.recentTasks : tasks.slice(0, 8)

  return (
    <div className="field-page supply-page">
      <div className="field-toolbar">
        <div>
          <span className="field-eyebrow">Supply Portal</span>
          <h2>Satınalma icmalı</h2>
        </div>
      </div>

      <section className="kpi-grid four">
        <KpiCard icon={<InboxOutlined />} title="Təyin edilmiş" value={formatNumber(dashboard?.assignedTasks ?? 0)} trend="mənim tapşırıqlarım" tone="blue" />
        <KpiCard icon={<ShoppingCartOutlined />} title="Alışdadır" value={formatNumber(dashboard?.shoppingTasks ?? 0)} trend="bazarda / tədarükdə" tone="orange" />
        <KpiCard icon={<UploadOutlined />} title="Təsdiqə göndərilib" value={formatNumber(dashboard?.submittedTasks ?? 0)} trend="rəhbər gözləyir" tone="purple" />
        <KpiCard icon={<ClockCircleOutlined />} title="Bildiriş" value={formatNumber(dashboard?.unreadNotifications ?? 0)} trend="oxunmamış" tone="green" />
      </section>

      <Card className="soft-card">
        <div className="card-heading">
          <h2>Son satınalma tapşırıqları</h2>
          <Tag color="blue">Sübut tələb olunur</Tag>
        </div>
        <Table
          rowKey="id"
          loading={loading}
          dataSource={rows}
          pagination={{ pageSize: 8 }}
          columns={[
            { title: 'Tapşırıq', dataIndex: 'code', render: (value, row) => <Link to={`/tasks/${row.id}`}><strong>{value}</strong></Link> },
            { title: 'Status', dataIndex: 'status', render: (value) => <Tag color={supplyStatusColor(value)}>{supplyStatusLabel(value)}</Tag> },
            { title: 'Prioritet', dataIndex: 'priority', render: (value) => priorityLabel(value) },
            { title: 'Sətir', render: (_, row) => row.lines.length },
            { title: 'Məbləğsiz ehtiyac', render: (_, row) => `${formatNumber(row.lines.reduce((sum, line) => sum + line.requestedQuantity, 0))} vahid` },
            { title: 'Yaradılıb', dataIndex: 'createdAt', render: (value) => new Date(value).toLocaleString('az-AZ') },
          ]}
        />
      </Card>
    </div>
  )
}
