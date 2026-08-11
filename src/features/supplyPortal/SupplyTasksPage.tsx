import { ReloadOutlined } from '@ant-design/icons'
import { Button, Card, Space, Table, Tag } from 'antd'
import { Link } from 'react-router-dom'
import { formatNumber } from '../../utils/formatters'
import { priorityLabel } from '../../utils/warehouseWorkflowLabels'
import { supplyStatusColor, supplyStatusLabel, useSupplyPortalStore } from './supplyPortalStore'

export const SupplyTasksPage = () => {
  const { tasks, loading, loadTasks } = useSupplyPortalStore()

  return (
    <div className="field-page supply-page">
      <div className="field-toolbar">
        <div>
          <span className="field-eyebrow">Satınalma icrası</span>
          <h2>Mənim tapşırıqlarım</h2>
        </div>
        <Button icon={<ReloadOutlined />} onClick={() => void loadTasks()}>Yenilə</Button>
      </div>
      <Card className="soft-card">
        <Table
          rowKey="id"
          loading={loading}
          dataSource={tasks}
          pagination={{ pageSize: 10 }}
          columns={[
            { title: 'Tapşırıq', dataIndex: 'code', render: (value, row) => <Link to={`/tasks/${row.id}`}><strong>{value}</strong></Link> },
            { title: 'Status', dataIndex: 'status', render: (value) => <Tag color={supplyStatusColor(value)}>{supplyStatusLabel(value)}</Tag> },
            { title: 'Prioritet', dataIndex: 'priority', render: (value) => priorityLabel(value) },
            { title: 'Materiallar', render: (_, row) => (
              <Space direction="vertical" size={2}>
                {row.lines.slice(0, 3).map((line) => <span key={line.id}>{line.itemName}: {formatNumber(line.requestedQuantity)} {line.unit}</span>)}
              </Space>
            ) },
            { title: 'Tələb olunan tarix', dataIndex: 'requiredBy', render: (value) => value || '-' },
            { title: 'Yaradılıb', dataIndex: 'createdAt', render: (value) => new Date(value).toLocaleString('az-AZ') },
          ]}
        />
      </Card>
    </div>
  )
}
