import { ArrowRightOutlined, CameraOutlined, CheckCircleOutlined, FileSearchOutlined, TeamOutlined } from '@ant-design/icons'
import { Button, Card, Col, Row, Space, Typography } from 'antd'

const appBaseUrl = (import.meta.env.VITE_APP_BASE_URL as string | undefined) ?? 'https://app.buildtrack.ferstaclabs.com'

const features = [
  { icon: <FileSearchOutlined />, title: 'Smeta və büdcə nəzarəti', text: 'Mərhələ, material və işçilik xərclərini real icra ilə müqayisə edin.' },
  { icon: <TeamOutlined />, title: 'Briqada və işçi idarəetməsi', text: 'İşçilər, briqadalar, saatlar və maaş hazırlığı bir tenant daxilində ayrılır.' },
  { icon: <CameraOutlined />, title: 'Dahua kamera inteqrasiyası', text: 'Üz tanıma terminallarından gələn girişlər avtomatik davamiyyətə çevrilir.' },
]

export const MarketingLandingPage = () => (
  <main className="marketing-page">
    <section className="marketing-hero">
      <nav className="marketing-nav">
        <div className="marketing-logo">BT</div>
        <Space>
          <Button href={`${appBaseUrl}/login`}>Giriş</Button>
          <Button type="primary" href={`${appBaseUrl}/register`}>Qeydiyyat</Button>
        </Space>
      </nav>
      <div className="marketing-hero-content">
        <Typography.Title>BuildTrack</Typography.Title>
        <Typography.Title level={2}>Tikinti layihələrini real vaxtda idarə edin</Typography.Title>
        <Typography.Paragraph>
          Smeta, material, işçi davamiyyəti, kamera inteqrasiyası, maaş və risk hesabatları bir B2B SaaS platformasında.
        </Typography.Paragraph>
        <Space wrap>
          <Button type="primary" size="large" href={`${appBaseUrl}/register`} icon={<ArrowRightOutlined />}>Qeydiyyatdan keç</Button>
          <Button size="large" href={`${appBaseUrl}/login`}>Giriş</Button>
        </Space>
      </div>
    </section>

    <section className="marketing-section">
      <Typography.Title level={2}>Tikinti sahəsində nəzarət boşluğu azalır</Typography.Title>
      <Row gutter={[18, 18]}>
        {['Əl ilə davamiyyət və gecikmiş maaş hesabatları', 'Material itkisi və smeta nəzarətsizliyi', 'Gecikən mərhələlər və riskli qərarlar'].map((item) => (
          <Col xs={24} md={8} key={item}>
            <Card className="marketing-card"><CheckCircleOutlined /> {item}</Card>
          </Col>
        ))}
      </Row>
    </section>

    <section className="marketing-section">
      <Typography.Title level={2}>Əsas imkanlar</Typography.Title>
      <Row gutter={[18, 18]}>
        {features.map((feature) => (
          <Col xs={24} md={8} key={feature.title}>
            <Card className="marketing-feature-card">
              <div className="marketing-feature-icon">{feature.icon}</div>
              <Typography.Title level={4}>{feature.title}</Typography.Title>
              <Typography.Paragraph>{feature.text}</Typography.Paragraph>
            </Card>
          </Col>
        ))}
      </Row>
    </section>

    <section className="marketing-camera">
      <Typography.Title level={2}>Kamera ilə davamiyyət</Typography.Title>
      <Typography.Paragraph>
        Dahua üz tanıma terminalları Active Register ilə BuildTrack backend-inə qoşulur. Tanınan işçilər davamiyyətə yazılır, şübhəli və tanınmayan üzlər ayrıca yoxlamaya düşür.
      </Typography.Paragraph>
    </section>

    <section className="marketing-section">
      <Typography.Title level={2}>Lisenziya paketləri</Typography.Title>
      <Row gutter={[18, 18]}>
        {['Starter', 'Business', 'Enterprise'].map((plan) => (
          <Col xs={24} md={8} key={plan}>
            <Card className="marketing-card">
              <Typography.Title level={3}>{plan}</Typography.Title>
              <Typography.Paragraph>Qiymət üçün əlaqə saxlayın.</Typography.Paragraph>
            </Card>
          </Col>
        ))}
      </Row>
    </section>
  </main>
)
