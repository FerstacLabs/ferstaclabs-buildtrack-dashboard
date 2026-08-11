import { ReloadOutlined } from '@ant-design/icons'
import { Button, Card, Space, Table, Tag } from 'antd'
import dayjs from 'dayjs'
import { Link } from 'react-router-dom'
import { ProcurementTaskStatusTag } from '../../components/ui/WarehouseWorkflowStatusTags'
import { formatNumber } from '../../utils/formatters'
import { priorityLabel } from '../../utils/warehouseWorkflowLabels'
import { useSupplyPortalStore } from './supplyPortalStore'

const requiredDateTag = (value?: string) => {
  if (!value) return '—'
  const date = dayjs(value)
  const today = dayjs().startOf('day')
  const days = date.startOf('day').diff(today, 'day')
  const color = days < 0 ? 'red' : days <= 2 ? 'orange' : 'blue'
  const suffix = days < 0 ? 'Gecikib' : days <= 2 ? 'Yaxınlaşır' : 'Planlı'
  return (
    <Space direction="vertical" size={2}>
      <span>{date.format('DD.MM.YYYY')}</span>
      <Tag color={color}>{suffix}</Tag>
    </Space>
  )
}

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
            { title: 'Status', render: (_, row) => <ProcurementTaskStatusTag status={row.status} /> },
            { title: 'Prioritet', dataIndex: 'priority', render: (value) => priorityLabel(value) },
            { title: 'Materiallar', render: (_, row) => (
              <Space direction="vertical" size={2}>
                {row.lines.slice(0, 3).map((line) => <span key={line.id}>{line.itemName}: {formatNumber(line.requestedQuantity)} {line.unit}</span>)}
              </Space>
            ) },
            { title: 'Tələb olunan tarix', dataIndex: 'requiredBy', render: (value) => requiredDateTag(value) },
            { title: 'Yaradılıb', dataIndex: 'createdAt', render: (value) => new Date(value).toLocaleString('az-AZ') },
            { title: 'Əməliyyat', render: (_, row) => <Button type="link"><Link to={`/tasks/${row.id}`}>Aç</Link></Button> },
          ]}
        />
      </Card>
    </div>
  )
}
