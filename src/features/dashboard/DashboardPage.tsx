import {
  AlertOutlined,
  ClockCircleOutlined,
  DollarCircleOutlined,
  FileSearchOutlined,
  ProjectOutlined,
  TeamOutlined,
  ToolOutlined,
} from '@ant-design/icons'
import { Progress, Table, Tag } from 'antd'
import type { TableColumnsType } from 'antd'
import { Link } from 'react-router-dom'
import {
  Bar,
  BarChart,
  CartesianGrid,
  Cell,
  Legend,
  Line,
  LineChart,
  Pie,
  PieChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts'
import { ObjectFilter } from '../../components/filters/ObjectFilter'
import { KpiCard } from '../../components/ui/KpiCard'
import { PageTitle } from '../../components/ui/PageTitle'
import type { WorkItem, WorkStage } from '../../types/projectProgress'
import { compactName, formatCurrency, formatHours, formatNumber, formatPercent } from '../../utils/formatters'
import {
  ALL_OBJECTS_ID,
  getCrewActualHours,
  getCrewsByObject,
  getDashboardSummary,
  getDailyReportsByObject,
  getEstimateRowsByObject,
  getMaterialsByObject,
  getStageActualHours,
  getStagesByObject,
  getWorkItemActualHours,
} from '../projectProgress/projectSelectors'
import { calculateStageProgress, statusColor, statusLabel, useProjectProgressStore } from '../projectProgress/projectProgressStore'

const costColors = ['#1479ff', '#078b55', '#7546c9']

const trendData = [
  { day: 'B.e', saat: 22 },
  { day: 'Ç.a', saat: 28 },
  { day: 'Ç', saat: 24 },
  { day: 'C.a', saat: 31 },
  { day: 'C', saat: 29 },
  { day: 'Ş', saat: 18 },
  { day: 'B', saat: 0 },
]

export const DashboardPage = () => {
  const data = useProjectProgressStore()
  const selectedObjectId = data.selectedObjectIdByPage.dashboard ?? ALL_OBJECTS_ID
  const metrics = getDashboardSummary(data, data.project.id, selectedObjectId)
  const scopedStages = getStagesByObject(data, selectedObjectId)
  const scopedWorkItems = getEstimateRowsByObject(data, selectedObjectId)
  const scopedCrews = getCrewsByObject(data, selectedObjectId)
  const scopedMaterials = getMaterialsByObject(data, selectedObjectId)
  const scopedReports = getDailyReportsByObject(data, selectedObjectId)
  const estimateTotals = {
    totalAmount: scopedStages.reduce((sum, stage) => sum + stage.totalCost, 0),
    laborAmount: scopedStages.reduce((sum, stage) => sum + stage.laborCost, 0),
    materialAmount: scopedStages.reduce((sum, stage) => sum + stage.materialCost, 0),
    hiddenCostAmount: selectedObjectId === ALL_OBJECTS_ID ? data.summary.hiddenCostAmount : data.summary.hiddenCostAmount / Math.max(1, data.objects.length),
  }
  const stageRows = scopedStages
    .map((stage) => ({
      ...stage,
      calculatedProgress: calculateStageProgress(stage, scopedWorkItems),
      derivedActualHours: getStageActualHours(data, stage.id) || stage.actualHours,
      crewName: data.crews.find((crew) => crew.id === stage.assignedCrewId)?.name ?? 'Təyin edilməyib',
    }))

  const workItems = scopedWorkItems.map((item) => ({
    ...item,
    actualHours: getWorkItemActualHours(data, item.id) || item.actualHours,
    stageName: data.stages.find((stage) => stage.id === item.stageId)?.name ?? 'Etap yoxdur',
    crewName: data.crews.find((crew) => crew.id === item.assignedCrewId)?.name ?? 'Təyin edilməyib',
  }))

  const activeWorkItems = workItems.filter((item) => ['InProgress', 'Paused', 'Delayed'].includes(item.status))
  const materialWarnings = scopedMaterials
    .filter((material) => material.quantity > 0 && material.remainingQuantity / material.quantity <= 0.15)
    .slice(0, 5)

  const crewHours = scopedCrews.map((crew) => {
    const relatedItems = scopedWorkItems.filter((item) => item.assignedCrewId === crew.id)
    return {
      name: compactName(crew.name, 15),
      plan: relatedItems.reduce((sum, item) => sum + item.plannedHours, 0),
      faktiki: getCrewActualHours(data, crew.id) || relatedItems.reduce((sum, item) => sum + item.actualHours, 0),
    }
  })

  const stageHourRows = stageRows.map((stage) => ({
    name: compactName(stage.name, 16),
    plan: stage.plannedHours,
    faktiki: stage.derivedActualHours,
  }))

  const costSplit = [
    { name: 'Material', value: estimateTotals.materialAmount },
    { name: 'İşçilik', value: estimateTotals.laborAmount },
    { name: 'Gizli xərc', value: estimateTotals.hiddenCostAmount },
  ]

  const stageColumns: TableColumnsType<WorkStage & { calculatedProgress: number; crewName: string }> = [
    { title: 'Etap', dataIndex: 'name', render: (value) => <strong>{value}</strong> },
    { title: 'Briqada', dataIndex: 'crewName' },
    { title: 'Plan tarix', render: (_, row) => `${row.plannedStartDate} - ${row.plannedEndDate}` },
    { title: 'Status', dataIndex: 'status', render: (value: WorkStage['status']) => <Tag color={statusColor[value]}>{statusLabel[value]}</Tag> },
    { title: 'Gedişat', dataIndex: 'calculatedProgress', render: (value) => <Progress percent={Number(value)} size="small" /> },
  ]

  const workColumns: TableColumnsType<WorkItem & { stageName: string; crewName: string }> = [
    { title: 'İş', dataIndex: 'name', render: (value, row) => <strong>{value}<br /><span className="muted-text">{row.stageName}</span></strong> },
    { title: 'Briqada', dataIndex: 'crewName' },
    { title: 'Plan/Fakt saat', render: (_, row) => `${formatHours(row.plannedHours, 0)} / ${formatHours(row.actualHours, 0)}` },
    { title: 'Status', dataIndex: 'status', render: (value: WorkItem['status']) => <Tag color={statusColor[value]}>{statusLabel[value]}</Tag> },
    { title: 'Gedişat', dataIndex: 'progressPercent', render: (value) => <Progress percent={Number(value)} size="small" /> },
  ]

  return (
    <div className="page-stack project-dashboard">
      <PageTitle
        title={data.project.name}
        subtitle="Smeta, briqada, iş saatı, material və prorab gündəlikləri üzrə layihə idarəetməsi"
        extra={<ObjectFilter pageKey="dashboard" />}
      />

      <section className="kpi-grid four">
        <KpiCard icon={<DollarCircleOutlined />} title="Yekun smeta" value={formatCurrency(estimateTotals.totalAmount)} tone="blue" />
        <KpiCard icon={<DollarCircleOutlined />} title="İşçilik büdcəsi" value={formatCurrency(estimateTotals.laborAmount)} tone="green" />
        <KpiCard icon={<ToolOutlined />} title="Material büdcəsi" value={formatCurrency(estimateTotals.materialAmount)} tone="orange" />
        <KpiCard icon={<AlertOutlined />} title="Gözə görünməyən xərclər" value={formatCurrency(estimateTotals.hiddenCostAmount)} tone="purple" />
      </section>

      <section className="kpi-grid four">
        <KpiCard icon={<ProjectOutlined />} title="Ümumi gedişat" value={formatPercent(metrics.weightedProgress, 1)} tone="green" />
        <KpiCard icon={<ClockCircleOutlined />} title="Plan saat" value={formatHours(metrics.plannedHours, 0)} tone="blue" />
        <KpiCard icon={<ClockCircleOutlined />} title="Faktiki saat" value={formatHours(metrics.actualHours, 0)} tone="orange" />
        <KpiCard icon={<ClockCircleOutlined />} title="Qalan saat" value={formatHours(metrics.remainingHours, 0)} tone="red" />
      </section>

      <section className="kpi-grid four">
        <KpiCard icon={<TeamOutlined />} title="Aktiv briqadalar" value={formatNumber(metrics.activeCrews)} tone="green" />
        <KpiCard icon={<AlertOutlined />} title="Gecikən işlər" value={formatNumber(metrics.delayedStages + metrics.delayedWorkItems)} tone="red" />
        <KpiCard icon={<FileSearchOutlined />} title="Bugünkü görülən işlər" value={formatNumber(metrics.todayReports)} tone="purple" />
        <KpiCard icon={<ClockCircleOutlined />} title="Bugünkü işçi saatları" value={formatHours(metrics.todayWorkerHours, 1)} tone="blue" />
      </section>

      <section className="project-chart-grid">
        <div className="chart-card">
          <div className="card-heading">
            <h2>Etaplar üzrə gedişat %</h2>
            <Link className="muted-text" to="/project-progress/timeline">Təqvimə bax</Link>
          </div>
          <div className="chart-body tall">
            <ResponsiveContainer>
              <BarChart data={stageRows.map((stage) => ({ name: compactName(stage.name, 18), progress: stage.calculatedProgress }))}>
                <CartesianGrid strokeDasharray="3 3" vertical={false} />
                <XAxis dataKey="name" tick={{ fontSize: 11 }} />
                <YAxis domain={[0, 100]} />
                <Tooltip formatter={(value) => `${value}%`} />
                <Bar dataKey="progress" fill="#1479ff" radius={[6, 6, 0, 0]} name="Gedişat %" />
              </BarChart>
            </ResponsiveContainer>
          </div>
        </div>

        <div className="chart-card">
          <div className="card-heading">
            <h2>Xərc bölgüsü</h2>
          </div>
          <div className="chart-body tall">
            <ResponsiveContainer>
              <PieChart>
                <Pie data={costSplit} innerRadius={58} outerRadius={92} paddingAngle={3} dataKey="value">
                  {costSplit.map((entry, index) => <Cell key={entry.name} fill={costColors[index]} />)}
                </Pie>
                <Tooltip formatter={(value) => formatCurrency(Number(value))} />
                <Legend />
              </PieChart>
            </ResponsiveContainer>
          </div>
        </div>
      </section>

      <section className="project-chart-grid">
        <div className="chart-card">
          <div className="card-heading">
            <h2>Plan saat vs faktiki saat</h2>
          </div>
          <div className="chart-body">
            <ResponsiveContainer>
              <BarChart data={stageHourRows}>
                <CartesianGrid strokeDasharray="3 3" vertical={false} />
                <XAxis dataKey="name" tick={{ fontSize: 11 }} />
                <YAxis />
                <Tooltip />
                <Legend />
                <Bar dataKey="plan" fill="#078b55" radius={[6, 6, 0, 0]} name="Plan saat" />
                <Bar dataKey="faktiki" fill="#ff8a00" radius={[6, 6, 0, 0]} name="Faktiki saat" />
              </BarChart>
            </ResponsiveContainer>
          </div>
        </div>

        <div className="chart-card">
          <div className="card-heading">
            <h2>Son 7 gün işçi saat trendi</h2>
          </div>
          <div className="chart-body">
            <ResponsiveContainer>
              <LineChart data={trendData}>
                <CartesianGrid strokeDasharray="3 3" vertical={false} />
                <XAxis dataKey="day" />
                <YAxis />
                <Tooltip />
                <Line type="monotone" dataKey="saat" stroke="#1479ff" strokeWidth={3} name="Saat" dot={{ r: 4 }} />
              </LineChart>
            </ResponsiveContainer>
          </div>
        </div>
      </section>

      <section className="project-chart-grid">
        <div className="chart-card">
          <div className="card-heading">
            <h2>Briqada saatları</h2>
            <Link className="muted-text" to="/crews">Briqadalara keç</Link>
          </div>
          <div className="chart-body">
            <ResponsiveContainer>
              <BarChart data={crewHours}>
                <CartesianGrid strokeDasharray="3 3" vertical={false} />
                <XAxis dataKey="name" tick={{ fontSize: 11 }} />
                <YAxis />
                <Tooltip />
                <Legend />
                <Bar dataKey="plan" fill="#078b55" radius={[6, 6, 0, 0]} name="Plan" />
                <Bar dataKey="faktiki" fill="#7546c9" radius={[6, 6, 0, 0]} name="Faktiki" />
              </BarChart>
            </ResponsiveContainer>
          </div>
        </div>

        <div className="table-card">
          <div className="card-heading">
            <h2>Material xəbərdarlıqları</h2>
            <Link className="muted-text" to="/materials">Materiallara keç</Link>
          </div>
          <div className="dashboard-warning-list">
            {materialWarnings.length ? materialWarnings.map((material) => (
              <div className="summary-metric" key={material.id}>
                <span>{material.name}</span>
                <strong>{formatNumber(material.remainingQuantity, 1)} {material.unit}</strong>
              </div>
            )) : <div className="empty-soft">Material çatışmazlığı yoxdur</div>}
          </div>
        </div>
      </section>

      <section className="table-card">
        <div className="card-heading">
          <h2>İcrada olan işlər</h2>
          <Link className="muted-text" to="/estimate">Smetanı redaktə et</Link>
        </div>
        <Table rowKey="id" columns={workColumns} dataSource={activeWorkItems} pagination={{ pageSize: 5 }} />
      </section>

      <section className="project-chart-grid">
        <div className="table-card">
          <div className="card-heading">
            <h2>Gecikən etaplar</h2>
          </div>
          <Table rowKey="id" columns={stageColumns} dataSource={stageRows.filter((stage) => ['Delayed', 'Paused'].includes(stage.status))} pagination={false} />
        </div>

        <div className="table-card">
          <div className="card-heading">
            <h2>Son prorab gündəlikləri</h2>
            <Link className="muted-text" to="/daily-reports">Gündəliklərə keç</Link>
          </div>
          <div className="daily-report-feed">
            {scopedReports.slice(0, 3).map((report) => (
              <div className="report-feed-item" key={report.id}>
                <strong>{report.date} · {report.foremanName}</strong>
                <span>{report.todayNotes}</span>
                <Tag color={report.status === 'Approved' ? 'green' : report.status === 'Submitted' ? 'blue' : 'default'}>{report.status}</Tag>
              </div>
            ))}
          </div>
        </div>
      </section>
    </div>
  )
}
