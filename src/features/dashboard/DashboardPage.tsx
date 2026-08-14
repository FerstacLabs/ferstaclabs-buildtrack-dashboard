import {
  ClockCircleOutlined,
  DollarCircleOutlined,
  ToolOutlined,
} from '@ant-design/icons'
import { Alert, Button, Progress, Skeleton, Table, Tag } from 'antd'
import type { TableColumnsType } from 'antd'
import type { ReactNode } from 'react'
import { useCallback, useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import {
  Bar,
  BarChart,
  CartesianGrid,
  Legend,
  Line,
  LineChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts'
import { WrappedAxisTick } from '../../components/charts/WrappedAxisTick'
import { ProjectSelect } from '../../components/ProjectSelect'
import { KpiCard } from '../../components/ui/KpiCard'
import { PageTitle } from '../../components/ui/PageTitle'
import { buildTrackBackendApi, type FieldDailyReport } from '../../services/api/buildTrackBackendApi'
import { ALL_PROJECTS_ID, useProjectSelectionStore } from '../../stores/projectSelectionStore'
import type { WorkItem, WorkStage } from '../../types/projectProgress'
import { compactName, formatCurrency, formatHours, formatNumber, formatPercent } from '../../utils/formatters'
import {
  dailyReportWorkSummary,
  fieldDailyReportStatusColor,
  fieldDailyReportStatusLabel,
  managementVisibleReportStatuses,
  sortFieldReportsNewestFirst,
  totalDailyReportLineValue,
} from '../dailyReports/dailyReportHelpers'
import {
  getCrewActualHours,
  getCrewsByObject,
  getDashboardSummary,
  getEstimateRowsByObject,
  getMaterialsByObject,
  getStageActualHours,
  getStagesByObject,
  getWorkItemActualHours,
} from '../projectProgress/projectSelectors'
import { calculateStageProgress, calculateWorkItemProgress, statusColor, statusLabel, useProjectProgressStore } from '../projectProgress/projectProgressStore'

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
  const selectedObjectId = useProjectSelectionStore((state) => state.selectedProjectId)
  const [recentReports, setRecentReports] = useState<FieldDailyReport[]>([])
  const [recentReportsLoading, setRecentReportsLoading] = useState(false)
  const [recentReportsError, setRecentReportsError] = useState<string | null>(null)
  const metrics = getDashboardSummary(data, data.project.id, selectedObjectId)
  const scopedStages = getStagesByObject(data, selectedObjectId)
  const scopedWorkItems = getEstimateRowsByObject(data, selectedObjectId)
  const scopedCrews = getCrewsByObject(data, selectedObjectId)
  const scopedMaterials = getMaterialsByObject(data, selectedObjectId)
  const isAllProjectsScope = selectedObjectId === ALL_PROJECTS_ID
  const estimateTotals = {
    totalAmount: scopedStages.reduce((sum, stage) => sum + stage.totalCost, 0),
    laborAmount: scopedStages.reduce((sum, stage) => sum + stage.laborCost, 0),
    materialAmount: scopedStages.reduce((sum, stage) => sum + stage.materialCost, 0),
  }
  const usedTotals = scopedWorkItems.reduce(
    (totals, item) => {
      const ratio = calculateWorkItemProgress(item) / 100
      return {
        totalAmount: totals.totalAmount + item.totalCost * ratio,
        laborAmount: totals.laborAmount + item.laborTotal * ratio,
        materialAmount: totals.materialAmount + item.materialTotal * ratio,
      }
    },
    { totalAmount: 0, laborAmount: 0, materialAmount: 0 },
  )
  const remainingTotals = {
    totalAmount: Math.max(0, estimateTotals.totalAmount - usedTotals.totalAmount),
    laborAmount: Math.max(0, estimateTotals.laborAmount - usedTotals.laborAmount),
    materialAmount: Math.max(0, estimateTotals.materialAmount - usedTotals.materialAmount),
  }
  const remainingHours = Math.max(0, metrics.plannedHours - metrics.actualHours)

  const stageRows = scopedStages.map((stage) => ({
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
    name: stage.name,
    plan: stage.plannedHours,
    faktiki: stage.derivedActualHours,
  }))

  const loadRecentReports = useCallback(async () => {
    setRecentReportsLoading(true)
    setRecentReportsError(null)
    try {
      const siteId = selectedObjectId === ALL_PROJECTS_ID ? undefined : selectedObjectId
      const reports = await buildTrackBackendApi.getManagementFieldReports(siteId)
      setRecentReports(
        sortFieldReportsNewestFirst(reports.filter((report) => managementVisibleReportStatuses.has(report.status))).slice(0, 4),
      )
    } catch (error) {
      setRecentReports([])
      setRecentReportsError(error instanceof Error ? error.message : 'Prorab gündəlikləri yüklənmədi.')
    } finally {
      setRecentReportsLoading(false)
    }
  }, [selectedObjectId])

  useEffect(() => {
    void loadRecentReports()
  }, [loadRecentReports])

  const keepObjectContext = (_pageKey: string) => undefined

  const linkedKpi = (to: string, pageKey: string, card: ReactNode) => (
    <Link className="kpi-link" to={to} onClick={keepObjectContext(pageKey)}>
      {card}
    </Link>
  )

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
        extra={<ProjectSelect pageKey="dashboard" />}
      />

      <section className="kpi-grid four">
        {linkedKpi('/estimate', 'estimate', <KpiCard icon={<DollarCircleOutlined />} title="Yekun smeta" value={formatCurrency(estimateTotals.totalAmount)} tone="blue" />)}
        {linkedKpi('/workers', 'workers', <KpiCard icon={<DollarCircleOutlined />} title="İşçilik büdcəsi" value={formatCurrency(estimateTotals.laborAmount)} tone="green" />)}
        {linkedKpi('/materials', 'materials', <KpiCard icon={<ToolOutlined />} title="Material büdcəsi" value={formatCurrency(estimateTotals.materialAmount)} tone="orange" />)}
        {linkedKpi('/timeline', 'timeline', <KpiCard icon={<ClockCircleOutlined />} title="Plan saat" value={formatHours(metrics.plannedHours, 0)} tone="blue" />)}

        {linkedKpi('/estimate', 'estimate', <KpiCard icon={<DollarCircleOutlined />} title="İstifadə olunan smeta" value={formatCurrency(usedTotals.totalAmount)} tone="green" />)}
        {linkedKpi('/payroll', 'payroll', <KpiCard icon={<DollarCircleOutlined />} title="İstifadə olunan işçilik büdcəsi" value={formatCurrency(usedTotals.laborAmount)} tone="green" />)}
        {linkedKpi('/materials', 'materials', <KpiCard icon={<ToolOutlined />} title="İstifadə olunan material büdcəsi" value={formatCurrency(usedTotals.materialAmount)} tone="green" />)}
        {linkedKpi('/daily-attendance', 'attendance', <KpiCard icon={<ClockCircleOutlined />} title="Faktiki saat" value={formatHours(metrics.actualHours, 0)} tone="orange" />)}

        {linkedKpi('/estimate', 'estimate', <KpiCard icon={<DollarCircleOutlined />} title="Qalıq smeta" value={formatCurrency(remainingTotals.totalAmount)} tone="purple" />)}
        {linkedKpi('/payroll', 'payroll', <KpiCard icon={<DollarCircleOutlined />} title="Qalıq işçilik büdcəsi" value={formatCurrency(remainingTotals.laborAmount)} tone="purple" />)}
        {linkedKpi('/materials', 'materials', <KpiCard icon={<ToolOutlined />} title="Qalıq material büdcəsi" value={formatCurrency(remainingTotals.materialAmount)} tone="purple" />)}
        {linkedKpi('/timeline', 'timeline', <KpiCard icon={<ClockCircleOutlined />} title="Qalan saat" value={formatHours(remainingHours, 0)} tone="red" />)}
      </section>

      <section className="project-chart-grid">
        <div className="chart-card">
          <div className="card-heading">
            <h2>Etaplar üzrə gedişat %</h2>
            <Link className="muted-text" to="/timeline" onClick={keepObjectContext('timeline')}>Təqvimə bax</Link>
          </div>
          <div className="chart-body tall">
            <ResponsiveContainer>
              <BarChart
                data={stageRows.map((stage) => ({ name: stage.name, progress: stage.calculatedProgress }))}
                margin={{ top: 12, right: 12, left: 0, bottom: 34 }}
              >
                <CartesianGrid strokeDasharray="3 3" vertical={false} />
                <XAxis
                  dataKey="name"
                  height={74}
                  interval={0}
                  tick={<WrappedAxisTick maxCharsPerLine={16} maxLines={2} />}
                  tickLine={false}
                  tickMargin={10}
                />
                <YAxis domain={[0, 100]} />
                <Tooltip formatter={(value) => `${value}%`} />
                <Bar dataKey="progress" fill="#1479ff" radius={[6, 6, 0, 0]} name="Gedişat %" />
              </BarChart>
            </ResponsiveContainer>
          </div>
        </div>

        <div className="chart-card">
          <div className="card-heading">
            <h2>Ümumi gedişat</h2>
            <Link className="muted-text" to="/timeline" onClick={keepObjectContext('timeline')}>Detallara bax</Link>
          </div>
          <div className="chart-body tall overall-progress-card">
            <Progress
              type="circle"
              percent={Number(metrics.weightedProgress.toFixed(1))}
              size={220}
              strokeColor="#078b55"
              trailColor="#e6edf4"
              format={(percent) => `${Number(percent ?? 0).toFixed(1)}%`}
            />
            <div className="summary-metric">
              <span>Seçilmiş layihə üzrə icra</span>
              <strong>{formatPercent(metrics.weightedProgress, 1)}</strong>
            </div>
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
              <BarChart data={stageHourRows} margin={{ top: 12, right: 12, left: 0, bottom: 34 }}>
                <CartesianGrid strokeDasharray="3 3" vertical={false} />
                <XAxis
                  dataKey="name"
                  height={74}
                  interval={0}
                  tick={<WrappedAxisTick maxCharsPerLine={16} maxLines={2} />}
                  tickLine={false}
                  tickMargin={10}
                />
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
              <LineChart data={trendData} margin={{ top: 12, right: 12, left: 0, bottom: 18 }}>
                <CartesianGrid strokeDasharray="3 3" vertical={false} />
                <XAxis
                  dataKey="day"
                  height={46}
                  interval={0}
                  tick={<WrappedAxisTick dy={14} maxCharsPerLine={5} maxLines={1} />}
                  tickLine={false}
                  tickMargin={8}
                />
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
            <Link className="muted-text" to="/crews" onClick={keepObjectContext('crews')}>Briqadalara keç</Link>
          </div>
          <div className="chart-body">
            <ResponsiveContainer>
              <BarChart data={crewHours} margin={{ top: 12, right: 12, left: 0, bottom: 26 }}>
                <CartesianGrid strokeDasharray="3 3" vertical={false} />
                <XAxis
                  dataKey="name"
                  height={62}
                  interval={0}
                  tick={<WrappedAxisTick maxCharsPerLine={14} maxLines={2} />}
                  tickLine={false}
                  tickMargin={10}
                />
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
            <Link className="muted-text" to="/materials" onClick={keepObjectContext('materials')}>Materiallara keç</Link>
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
          <Link className="muted-text" to="/estimate" onClick={keepObjectContext('estimate')}>Smetanı redaktə et</Link>
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
            <Link className="muted-text" to="/daily-reports" onClick={keepObjectContext('dailyReports')}>Gündəliklərə keç</Link>
          </div>
          <div className="daily-report-feed">
            {recentReportsLoading ? (
              <Skeleton active paragraph={{ rows: 5 }} title={false} />
            ) : recentReportsError ? (
              <Alert
                type="error"
                showIcon
                message="Prorab gündəlikləri yüklənmədi."
                description={recentReportsError}
                action={<Button size="small" onClick={() => void loadRecentReports()}>Yenilə</Button>}
              />
            ) : recentReports.length ? recentReports.map((report) => {
              const workerCount = totalDailyReportLineValue(report.lines, 'workerCount')
              const workHours = totalDailyReportLineValue(report.lines, 'workHours')
              return (
                <div className="report-feed-item" key={report.id} data-i18n-skip="true">
                  <strong>{report.reportDate} · {report.supervisorName || 'Prorab qeyd edilməyib'}</strong>
                  {isAllProjectsScope ? <span className="muted-text">{report.siteName || 'Layihə qeyd edilməyib'}</span> : null}
                  <span>{dailyReportWorkSummary(report.lines)}</span>
                  <span className="muted-text">{formatNumber(workerCount)} işçi · {formatNumber(workHours)} saat</span>
                  {report.generalNote ? <span className="report-feed-note">{report.generalNote}</span> : null}
                  <Tag color={fieldDailyReportStatusColor[report.status]}>{fieldDailyReportStatusLabel[report.status]}</Tag>
                </div>
              )
            }) : (
              <div className="empty-soft">
                {isAllProjectsScope ? 'Hələ prorab gündəliyi daxil edilməyib.' : 'Seçilmiş layihə üzrə prorab gündəliyi yoxdur.'}
              </div>
            )}
          </div>
        </div>
      </section>
    </div>
  )
}
