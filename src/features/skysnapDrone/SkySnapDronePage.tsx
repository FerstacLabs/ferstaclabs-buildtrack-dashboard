import { ApiOutlined, CloudOutlined, ExportOutlined, GlobalOutlined, SafetyCertificateOutlined } from '@ant-design/icons'
import { Alert, Button, Card, Col, Empty, Row, Space, Statistic, Tag, Typography } from 'antd'
import { useMemo } from 'react'
import { PageTitle } from '../../components/ui/PageTitle'
import { useI18n } from '../../i18n'

const normalizeEmbedUrl = (value?: string) => {
  const trimmed = value?.trim() ?? ''
  if (!trimmed || trimmed === 'https://placeholder.example.com') return ''
  return trimmed
}

const isEmbedEnabled = (value?: string) => value?.trim().toLowerCase() === 'true'

export const SkySnapDronePage = () => {
  const { t } = useI18n()
  const embedUrl = useMemo(() => normalizeEmbedUrl(import.meta.env.VITE_SKYSNAP_EMBED_URL), [])
  const embedEnabled = isEmbedEnabled(import.meta.env.VITE_SKYSNAP_EMBED_ENABLED)
  const canRenderIframe = embedEnabled && Boolean(embedUrl)

  return (
    <div className="page-stack">
      <PageTitle
        title={t('skysnap.title')}
        subtitle={t('skysnap.subtitle')}
        extra={
          embedUrl ? (
            <Button type="primary" icon={<ExportOutlined />} href={embedUrl} target="_blank" rel="noreferrer">
              {t('skysnap.openNewTab')}
            </Button>
          ) : null
        }
      />

      <Alert
        type="info"
        showIcon
        message={t('skysnap.storyTitle')}
        description={t('skysnap.storyText')}
      />

      <Row gutter={[16, 16]}>
        <Col xs={24} md={12} xl={6}>
          <Card className="soft-card">
            <Statistic title={t('skysnap.kpi.capture')} value={18} suffix={t('skysnap.kpi.flights')} prefix={<CloudOutlined />} />
          </Card>
        </Col>
        <Col xs={24} md={12} xl={6}>
          <Card className="soft-card">
            <Statistic title={t('skysnap.kpi.progress')} value={63} suffix="%" prefix={<GlobalOutlined />} />
          </Card>
        </Col>
        <Col xs={24} md={12} xl={6}>
          <Card className="soft-card">
            <Statistic title={t('skysnap.kpi.evidence')} value={42} suffix={t('skysnap.kpi.photos')} prefix={<SafetyCertificateOutlined />} />
          </Card>
        </Col>
        <Col xs={24} md={12} xl={6}>
          <Card className="soft-card">
            <Statistic title={t('skysnap.kpi.insights')} value={7} prefix={<ApiOutlined />} />
          </Card>
        </Col>
      </Row>

      <Card className="table-card skysnap-embed-card">
        <div className="card-heading">
          <div>
            <h2>{t('skysnap.embedTitle')}</h2>
            <p>{t('skysnap.embedDescription')}</p>
          </div>
          <Space wrap>
            <Tag color="blue">FerstacLabs</Tag>
            <Tag color="green">1Muhasib</Tag>
            <Tag color="purple">SkySnap</Tag>
          </Space>
        </div>

        {!embedUrl ? (
          <Empty
            image={Empty.PRESENTED_IMAGE_SIMPLE}
            description={
              <Space direction="vertical" size={4}>
                <Typography.Text strong>{t('skysnap.notConfigured')}</Typography.Text>
                <Typography.Text type="secondary">{t('skysnap.configureHint')}</Typography.Text>
              </Space>
            }
          />
        ) : canRenderIframe ? (
          <iframe
            className="skysnap-embed-frame"
            title="SkySnap drone progress"
            src={embedUrl}
            sandbox="allow-forms allow-popups allow-popups-to-escape-sandbox allow-same-origin allow-scripts"
            allow="fullscreen; geolocation"
            referrerPolicy="no-referrer"
          />
        ) : (
          <div className="skysnap-launch-card">
            <div className="skysnap-launch-copy">
              <Typography.Title level={3}>{t('skysnap.launchTitle')}</Typography.Title>
              <Typography.Paragraph>{t('skysnap.launchDescription')}</Typography.Paragraph>
              <div className="skysnap-partner-strip">
                <span>FerstacLabs</span>
                <span>1Muhasib</span>
                <span>SkySnap</span>
              </div>
              <Typography.Text type="secondary">{t('skysnap.embedDisabledNote')}</Typography.Text>
            </div>
            <Button type="primary" size="large" icon={<ExportOutlined />} href={embedUrl} target="_blank" rel="noopener noreferrer">
              {t('skysnap.openPlatform')}
            </Button>
          </div>
        )}
      </Card>
    </div>
  )
}
