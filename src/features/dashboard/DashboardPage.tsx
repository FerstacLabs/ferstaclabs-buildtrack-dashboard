import {
  AuditOutlined,
  BarChartOutlined,
  CalendarOutlined,
  ClockCircleOutlined,
  DollarCircleOutlined,
  ExportOutlined,
  FileSearchOutlined,
  ImportOutlined,
  LineChartOutlined,
  SafetyCertificateOutlined,
  TeamOutlined,
} from '@ant-design/icons'
import { Link } from 'react-router-dom'
import { FilterBar } from '../../components/layout/FilterBar'
import { ExplanationCard } from '../../components/ui/ExplanationCard'
import { KpiCard } from '../../components/ui/KpiCard'
import { PageTitle } from '../../components/ui/PageTitle'
import { dashboardSummary } from '../../services/data/reportCalculations'
import { useBuildTrackStore } from '../../services/data/dataService'
import { formatCurrency, formatHours, formatNumber } from '../../utils/formatters'

const modules = [
  {
    title: 'Günlük Davamiyyət',
    path: '/daily-attendance',
    text: 'İşçilərin gündəlik giriş-çıxış, status və davamiyyət monitorinqi.',
    icon: <CalendarOutlined />,
    tone: '#078b55',
  },
  {
    title: 'Obyekt Saatları',
    path: '/site-hours',
    text: 'Obyektlər üzrə plan-fakt saatlar və effektivlik təhlili.',
    icon: <ClockCircleOutlined />,
    tone: '#1479ff',
  },
  {
    title: 'Riskli İşçilər',
    path: '/risk-workers',
    text: 'Riskli işçilərin müəyyən edilməsi və səbəblərin təhlili.',
    icon: <SafetyCertificateOutlined />,
    tone: '#ff8a00',
  },
  {
    title: 'Gecikmələr və İcazələr',
    path: '/delays-permissions',
    text: 'Gecikmə və icazə statistikası, səbəblər və təsdiqlər.',
    icon: <ClockCircleOutlined />,
    tone: '#078b55',
  },
  {
    title: 'Maaş Hesabatı',
    path: '/payroll',
    text: 'İş saatları və tariflərə əsasən maaş hesablamaları.',
    icon: <DollarCircleOutlined />,
    tone: '#7546c9',
  },
  {
    title: 'Performans Trendi',
    path: '/performance',
    text: 'Davamiyyət, saat və performans trend analitikası.',
    icon: <LineChartOutlined />,
    tone: '#1479ff',
  },
  {
    title: 'Prorab Audit',
    path: '/supervisor-audit',
    text: 'Prorab və briqadalar üzrə audit və uyğunluq qiymətləndirilməsi.',
    icon: <AuditOutlined />,
    tone: '#078b55',
  },
  {
    title: 'İş Fazası & Cost Code',
    path: '/cost-code',
    text: 'İş fazaları və cost code üzrə plan-fakt və xərc nəzarəti.',
    icon: <BarChartOutlined />,
    tone: '#1479ff',
  },
  {
    title: 'Custom Reports',
    path: '/custom-reports',
    text: 'İstədiyiniz hesabatları yaradın, filtrəyin və ixrac edin.',
    icon: <FileSearchOutlined />,
    tone: '#7546c9',
  },
]

export const DashboardPage = () => {
  const { data, filters } = useBuildTrackStore()
  if (!data) return null

  const summary = dashboardSummary(data, filters)

  return (
    <div className="page-stack">
      <PageTitle title="Xoş gəlmisiniz!" subtitle="İşçi Davamiyyət və Maaş Nəzarət Platforması" />
      <FilterBar data={data} />

      <section className="kpi-grid five">
        <KpiCard icon={<TeamOutlined />} title="Aktiv işçi" value={formatNumber(summary.activeWorkers)} trend="12 (öncəki aya nisbətən)" tone="green" />
        <KpiCard icon={<TeamOutlined />} title="Gəlməyən" value={formatNumber(summary.absent)} trend="4 (öncəki aya nisbətən)" tone="red" />
        <KpiCard icon={<SafetyCertificateOutlined />} title="Riskli" value={formatNumber(summary.risk)} trend="5 (öncəki aya nisbətən)" tone="orange" />
        <KpiCard icon={<ClockCircleOutlined />} title="Toplam saat" value={formatHours(summary.totalHours, 0)} trend="8% (öncəki aya nisbətən)" tone="blue" />
        <KpiCard icon={<DollarCircleOutlined />} title="Əmək xərci" value={formatCurrency(summary.laborCost)} trend="6% (öncəki aya nisbətən)" tone="green" />
      </section>

      <section className="table-card">
        <div className="card-heading">
          <h2>9 Əsas Hesabat Modulu — İcmal Baxış</h2>
        </div>
        <div className="module-grid">
          {modules.map((module, index) => (
            <Link className="module-card" to={module.path} key={module.path}>
              <span className="module-index" style={{ background: module.tone }}>
                {index + 1}
              </span>
              <div>
                <h3>{module.title}</h3>
                <p>{module.text}</p>
              </div>
              <span className="module-arrow">{module.icon}</span>
            </Link>
          ))}
        </div>
      </section>

      <section className="bottom-flow">
        <div className="table-card">
          <div className="card-heading">
            <h2>Sistem Axını</h2>
          </div>
          <div className="flow-row">
            {[
              ['Excel Import', 'İşçilərin və obyektlərin Excel fayldan yüklənməsi.', <ImportOutlined />],
              ['İşçi və Obyekt Təyini', 'Briqada və obyektlərə təyin prosesi.', <TeamOutlined />],
              ['Geofence + Offline', 'Mobil və tablet məlumat toplama modeli.', <ClockCircleOutlined />],
              ['Hesabatlar', 'Saat, risk və maaş hesablamaları.', <BarChartOutlined />],
              ['1C Export', 'Maaş hesabı və sistemlərə ixrac.', <ExportOutlined />],
            ].map(([title, text, icon]) => (
              <Link to={title === 'Excel Import' ? '/import' : '/export'} className="flow-card" key={String(title)}>
                <div className="kpi-icon kpi-green">{icon}</div>
                <strong>{title}</strong>
                <span>{text}</span>
              </Link>
            ))}
          </div>
        </div>

        <ExplanationCard icon={<SafetyCertificateOutlined />} title="Yan Menyu və İstifadə Rahatlığı">
          <ul>
            <li>Aydın və modul əsaslı naviqasiya.</li>
            <li>Ən çox istifadə olunan hesabatlara bir kliklə keçid.</li>
            <li>Rol və komanda ehtiyaclarına uyğun sadə görünüş.</li>
          </ul>
        </ExplanationCard>
      </section>

      <section className="table-card">
        <div className="card-heading">
          <h2>Kimlər üçün nəzərdə tutulub?</h2>
        </div>
        <div className="audience-grid">
          {[
            ['Menecerlər', 'Davamiyyət, əmək xərci və riskləri real vaxtda izləyin.'],
            ['Layihə rəhbərləri', 'Obyekt və briqada nəzarətini sadələşdirin.'],
            ['Mühasibatlıq', 'Dəqiq saat məlumatı əsasında maaş hesablayın və 1C-yə ötürün.'],
          ].map(([title, text]) => (
            <div className="panel-card" key={title}>
              <h2>{title}</h2>
              <p>{text}</p>
            </div>
          ))}
        </div>
      </section>
    </div>
  )
}
