import { Alert, Card, Descriptions, List, Skeleton, Typography } from 'antd'
import { useFieldPortalStore } from './fieldPortalStore'

const DASH = '—'

const fieldRoleLabel = (role?: string) => {
  if (role === 'Supervisor') return 'Prorab'
  return role?.trim() || DASH
}

const valueOrDash = (value?: string) => value?.trim() || DASH

const formatAssignmentDate = (value?: string) => {
  if (!value) return DASH
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? DASH : date.toLocaleDateString('az-AZ')
}

export const FieldSettingsPage = () => {
  const me = useFieldPortalStore((state) => state.me)
  const assignments = useFieldPortalStore((state) => state.assignments)
  const loading = useFieldPortalStore((state) => state.loading)
  const error = useFieldPortalStore((state) => state.error)

  if (loading && !me) {
    return (
      <div className="field-page">
        <Skeleton active paragraph={{ rows: 6 }} />
      </div>
    )
  }

  if (error) {
    return (
      <div className="field-page">
        <Alert type="error" showIcon message="Hesab məlumatları yüklənmədi" description={error} />
      </div>
    )
  }

  return (
    <div className="field-page">
      <div className="field-page-title">
        <span>Prorab hesabı</span>
        <Typography.Title level={2}>Ayarlar</Typography.Title>
      </div>
      <Card className="soft-card" title="Hesab məlumatları">
        <Descriptions column={1}>
          <Descriptions.Item label="Ad Soyad">{valueOrDash(me?.fullName)}</Descriptions.Item>
          <Descriptions.Item label="Email">{valueOrDash(me?.email)}</Descriptions.Item>
          <Descriptions.Item label="Rol">{fieldRoleLabel(me?.role)}</Descriptions.Item>
          <Descriptions.Item label="Şirkət">{valueOrDash(me?.tenantName)}</Descriptions.Item>
        </Descriptions>
      </Card>
      <Card className="soft-card" title="Təyin olunmuş obyektlər">
        <List
          locale={{ emptyText: 'Təyin olunmuş obyekt yoxdur' }}
          dataSource={assignments}
          renderItem={(assignment) => (
            <List.Item>
              <List.Item.Meta
                title={valueOrDash(assignment.siteName)}
                description={assignment.address?.trim() || 'Ünvan qeyd edilməyib'}
              />
              <span>{formatAssignmentDate(assignment.assignedAt)}</span>
            </List.Item>
          )}
        />
      </Card>
    </div>
  )
}
