import { Empty, Table } from 'antd'
import type { ReactNode } from 'react'
import type { TableColumnsType } from 'antd'

interface DataTableProps<T extends object> {
  title: string
  columns: TableColumnsType<T>
  data: T[]
  extra?: ReactNode
  emptyText?: ReactNode
  pageSize?: number
}

export const DataTable = <T extends object>({ columns, data, emptyText, extra, pageSize = 10, title }: DataTableProps<T>) => (
  <section className="table-card">
    <div className="card-heading">
      <h2>{title}</h2>
      {extra}
    </div>
    <Table<T>
      columns={columns}
      dataSource={data}
      locale={{ emptyText: emptyText ?? <Empty description="Məlumat yoxdur" /> }}
      pagination={{ pageSize, showSizeChanger: true, pageSizeOptions: [10, 15, 20, 50] }}
      rowKey="key"
      scroll={{ x: 'max-content' }}
      size="middle"
    />
  </section>
)


