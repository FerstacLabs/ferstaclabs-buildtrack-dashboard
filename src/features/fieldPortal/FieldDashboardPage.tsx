import { CalendarOutlined, FileDoneOutlined, InboxOutlined, TeamOutlined } from '@ant-design/icons'
import { Alert, Card, Col, List, Row, Skeleton, Space, Statistic, Tag, Typography } from 'antd'
import { useEffect, useState } from 'react'
import { buildTrackBackendApi, type FieldDashboard } from '../../services/api/buildTrackBackendApi'
import { fieldStatusColor, fieldStatusLabel, useFieldPortalStore } from './fieldPortalStore'

export const FieldDashboardPage = () => {
  const selectedSiteId = useFieldPortalStore((state) => state.selectedSiteId)
  const [dashboard, setDashboard] = useState<FieldDashboard>()
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string>()

  useEffect(() => {
    if (!selectedSiteId) return
    let cancelled = false
    setLoading(true)
    buildTrackBackendApi.getFieldDashboard(selectedSiteId)
      .then((data) => {
        if (!cancelled) setDashboard(data)
      })
      .catch((err) => {
        if (!cancelled) setError(err instanceof Error ? err.message : 'İcmal yüklənmədi')
      })
      .finally(() => {
        if (!cancelled) setLoading(false)
      })
    return () => {
      cancelled = true
    }
  }, [selectedSiteId])

  if (!selectedSiteId) return <Alert type="info" showIcon message="Layihə seçin" />
  if (loading && !dashboard) return <Skeleton active />

  return (
    <div className="field-page">
      <div className="field-page-title">
        <span>Prorab icmalı</span>
        <Typography.Title level={2}>{dashboard?.siteName ?? 'Layihə'}</Typography.Title>
        <Typography.Paragraph>Gündəlik hesabat, işçi qeydləri və anbar sorğuları üçün sahə paneli.</Typography.Paragraph>
      </div>
      {error && <Alert type="error" showIcon message={error} />}
      <Row gutter={[16, 16]}>
        <Col xs={24} md={12} xl={6}>
          <Card className="field-kpi-card"><Statistic title="Aktiv işçi" value={dashboard?.activeWorkers ?? 0} prefix={<TeamOutlined />} /></Card>
        </Col>
        <Col xs={24} md={12} xl={6}>
          <Card className="field-kpi-card"><Statistic title="Bugünkü hesabat" value={dashboard?.todayReports ?? 0} prefix={<FileDoneOutlined />} /></Card>
        </Col>
        <Col xs={24} md={12} xl={6}>
          <Card className="field-kpi-card"><Statistic title="Təsdiq gözləyən" value={dashboard?.pendingReports ?? 0} prefix={<CalendarOutlined />} /></Card>
        </Col>
        <Col xs={24} md={12} xl={6}>
          <Card className="field-kpi-card"><Statistic title="Açıq anbar sorğusu" value={dashboard?.openWarehouseRequests ?? 0} prefix={<InboxOutlined />} /></Card>
        </Col>
      </Row>
      <Row gutter={[16, 16]}>
        <Col xs={24} lg={14}>
          <Card title="Son fəaliyyət" className="soft-card">
            <List
              dataSource={dashboard?.recentActivity ?? []}
              locale={{ emptyText: 'Hələ fəaliyyət yoxdur' }}
              renderItem={(item) => (
                <List.Item>
                  <Space direction="vertical" size={2}>
                    <Typography.Text strong>{item.title}</Typography.Text>
                    <Typography.Text type="secondary">{new Date(item.timestamp).toLocaleString('az-AZ')}</Typography.Text>
                  </Space>
                  <Tag color={fieldStatusColor(item.status)}>{fieldStatusLabel(item.status)}</Tag>
                </List.Item>
              )}
            />
          </Card>
        </Col>
        <Col xs={24} lg={10}>
          <Card title="Sahə qaydası" className="soft-card">
            <Typography.Paragraph>
              Bu portalda prorab yalnız ona təyin olunmuş layihə üzrə faktiki görülən iş miqdarını, sahə qeydlərini və material sorğularını daxil edir.
            </Typography.Paragraph>
            <Typography.Paragraph>
              Smeta dəyərləri, material balansı və əmək haqqı hesablamaları idarəetmə panelində qalır.
            </Typography.Paragraph>
          </Card>
        </Col>
      </Row>
    </div>
  )
}
