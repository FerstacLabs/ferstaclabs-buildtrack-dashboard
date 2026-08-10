import { Card, Table, Tag } from 'antd'
import { supplyStatusColor, supplyStatusLabel, useSupplyPortalStore } from './supplyPortalStore'

export const SupplyHistoryPage = () => {
  const { tasks, loading } = useSupplyPortalStore()
  const rows = tasks.filter((task) => ['Completed', 'Verified', 'Cancelled'].includes(task.status))

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
            { title: 'Task', dataIndex: 'code' },
            { title: 'Status', dataIndex: 'status', render: (value) => <Tag color={supplyStatusColor(value)}>{supplyStatusLabel(value)}</Tag> },
            { title: 'Təsdiq', dataIndex: 'verifiedAt', render: (value) => value ? new Date(value).toLocaleString('az-AZ') : '-' },
            { title: 'Qeyd', dataIndex: 'verificationNote', render: (value) => value || '-' },
          ]}
        />
      </Card>
    </div>
  )
}
