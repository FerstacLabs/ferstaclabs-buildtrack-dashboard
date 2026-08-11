import { Button, Card, Space, Table, Tag } from 'antd'
import { Link } from 'react-router-dom'
import { formatCurrency, formatNumber } from '../../utils/formatters'
import { supplyStatusColor, supplyStatusLabel, useSupplyPortalStore } from './supplyPortalStore'

export const SupplyHistoryPage = () => {
  const { tasks, loading } = useSupplyPortalStore()
  const rows = tasks.filter((task) => ['SubmittedForVerification', 'Completed', 'Verified', 'Cancelled'].includes(task.status))

  return (
    <div className="field-page supply-page">
      <div className="field-toolbar">
        <div>
          <span className="field-eyebrow">Satınalma arxivi</span>
          <h2>Tarixçə</h2>
        </div>
      </div>
      <Card className="soft-card">
        <Table
          rowKey="id"
          loading={loading}
          dataSource={rows}
          pagination={{ pageSize: 10 }}
          columns={[
            { title: 'Tapşırıq', dataIndex: 'code' },
            { title: 'Tarix', render: (_, row) => new Date(row.submittedAt || row.verifiedAt || row.createdAt).toLocaleString('az-AZ') },
            { title: 'Materiallar', render: (_, row) => (
              <Space direction="vertical" size={2}>
                {row.lines.slice(0, 3).map((line) => <span key={line.id}>{line.itemName}: {formatNumber(line.purchasedQuantity || line.requestedQuantity)} {line.unit}</span>)}
              </Space>
            ) },
            { title: 'Toplam məbləğ', render: (_, row) => formatCurrency(row.lines.reduce((sum, line) => sum + (line.unitPrice ? line.unitPrice * line.purchasedQuantity : 0), 0)) },
            { title: 'Status', dataIndex: 'status', render: (value) => <Tag color={supplyStatusColor(value)}>{supplyStatusLabel(value)}</Tag> },
            { title: 'Qeyd', dataIndex: 'verificationNote', render: (value) => value || '—' },
            { title: 'Əməliyyat', render: (_, row) => <Button type="link"><Link to={`/tasks/${row.id}`}>Bax</Link></Button> },
          ]}
        />
      </Card>
    </div>
  )
}
