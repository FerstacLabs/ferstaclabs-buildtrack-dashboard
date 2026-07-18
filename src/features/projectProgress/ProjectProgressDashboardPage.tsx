import { ClockCircleOutlined, DollarCircleOutlined, ExclamationCircleOutlined, FileSearchOutlined, TeamOutlined } from '@ant-design/icons'
import { Progress, Table, Tag } from 'antd'
import type { TableColumnsType } from 'antd'
import { useEffect } from 'react'
import { Bar, BarChart, CartesianGrid, Cell, Legend, Pie, PieChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import { KpiCard } from '../../components/ui/KpiCard'
import { PageTitle } from '../../components/ui/PageTitle'
import type { WorkStage } from '../../types/projectProgress'
import { compactName, formatHours, formatNumber, formatPercent } from '../../utils/formatters'
import { projectProgressApi } from './projectProgressApi'
import { calculateProjectMetrics, calculateStageProgress, statusColor, statusLabel, useProjectProgressStore } from './projectProgressStore'

const formatAzn = (value: number) => `${formatNumber(value, value % 1 === 0 ? 0 : 2)} AZN`

const costColors = ['#1479ff', '#078b55', '#7546c9']

export const ProjectProgressDashboardPage = () => {
  const data = useProjectProgressStore()
  const applyBackendData = useProjectProgressStore((state) => state.applyBackendData)

  useEffect(() => {
    let cancelled = false
    const hydrate = async () => {
      const [summary, stages, workItems, crews] = await Promise.all([
        projectProgressApi.getSummary(),
        projectProgressApi.getStages(),
        projectProgressApi.getWorkItems(),
        projectProgressApi.getCrews(),
      ])

      if (cancelled) return
      if (summary || stages?.length || workItems?.length || crews?.length) {
        applyBackendData({ summary, stages, workItems, crews })
      }
    }

    void hydrate()
    return () => {
      cancelled = true
    }
  }, [applyBackendData])
  const metrics = calculateProjectMetrics(data)
  const stageRows = data.stages
    .slice()
    .sort((a, b) => a.order - b.order)
    .map((stage) => ({
      ...stage,
      calculatedProgress: calculateStageProgress(stage, data.workItems),
      crewName: data.crews.find((crew) => crew.id === stage.assignedCrewId)?.name ?? 'Təyin edilməyib',
    }))

  const crewHours = data.crews.map((crew) => {
    const crewStages = data.stages.filter((stage) => stage.assignedCrewId === crew.id)
    return {
      name: compactName(crew.name, 14),
      plan: crewStages.reduce((sum, stage) => sum + stage.plannedHours, 0),
      faktiki: crewStages.reduce((sum, stage) => sum + stage.actualHours, 0),
    }
  })

  const costSplit = [
    { name: 'İşçilik', value: data.summary.laborAmount },
    { name: 'Material', value: data.summary.materialAmount },
    { name: 'Gizli xərclər', value: data.summary.hiddenCostAmount },
  ]

  const columns: TableColumnsType<WorkStage & { calculatedProgress: number; crewName: string }> = [
    { title: 'Etap', dataIndex: 'name', render: (value) => <strong>{value}</strong> },
    { title: 'Briqada', dataIndex: 'crewName' },
    { title: 'Məbləğ', dataIndex: 'totalCost', align: 'right', render: (value) => formatAzn(Number(value)) },
    { title: 'Status', dataIndex: 'status', render: (value) => <Tag color={statusColor[value as WorkStage['status']]}>{statusLabel[value as WorkStage['status']]}</Tag> },
    { title: 'Gedişat', dataIndex: 'calculatedProgress', render: (value) => <Progress percent={Number(value)} size="small" /> },
  ]

  return (
    <div className="page-stack project-progress-page">
      <PageTitle title="Layihə Gedişatı" subtitle="Villa smetası əsasında plan-fakt, briqada və material gedişatı" />

      <section className="kpi-grid four">
        <KpiCard icon={<DollarCircleOutlined />} title="Yekun smeta" value={formatAzn(data.summary.totalAmount)} tone="blue" />
        <KpiCard icon={<DollarCircleOutlined />} title="İşçilik" value={formatAzn(data.summary.laborAmount)} tone="green" />
        <KpiCard icon={<FileSearchOutlined />} title="Material" value={formatAzn(data.summary.materialAmount)} tone="orange" />
        <KpiCard icon={<ExclamationCircleOutlined />} title="Gözə görünməyən xərclər" value={formatAzn(data.summary.hiddenCostAmount)} tone="purple" />
      </section>

      <section className="kpi-grid four">
        <KpiCard icon={<FileSearchOutlined />} title="Ümumi gedişat faizi" value={formatPercent(metrics.weightedProgress, 1)} tone="green" />
        <KpiCard icon={<TeamOutlined />} title="Aktiv briqadalar" value={formatNumber(metrics.activeCrews)} tone="blue" />
        <KpiCard icon={<ExclamationCircleOutlined />} title="Gecikən etaplar" value={formatNumber(metrics.delayedStages)} tone="red" />
        <KpiCard icon={<ClockCircleOutlined />} title="Plan / Faktiki saat" value={`${formatHours(metrics.plannedHours, 0)} / ${formatHours(metrics.actualHours, 0)}`} tone="orange" />
      </section>

      <section className="project-chart-grid">
        <div className="chart-card">
          <div className="card-heading">
            <h2>Etaplar üzrə gedişat</h2>
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
                <Tooltip formatter={(value) => formatAzn(Number(value))} />
                <Legend />
              </PieChart>
            </ResponsiveContainer>
          </div>
        </div>
      </section>

      <section className="project-chart-grid">
        <div className="chart-card">
          <div className="card-heading">
            <h2>Briqada saatları: plan vs faktiki</h2>
          </div>
          <div className="chart-body">
            <ResponsiveContainer>
              <BarChart data={crewHours}>
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

        <div className="table-card">
          <div className="card-heading">
            <h2>Təqvim xülasəsi</h2>
          </div>
          <div className="timeline-list compact">
            {stageRows.map((stage) => (
              <div className="timeline-row" key={stage.id}>
                <div>
                  <strong>{stage.order}. {stage.name}</strong>
                  <span>{stage.plannedStartDate} - {stage.plannedEndDate} · {stage.crewName}</span>
                </div>
                <Progress percent={stage.calculatedProgress} size="small" />
              </div>
            ))}
          </div>
        </div>
      </section>

      <section className="table-card">
        <div className="card-heading">
          <h2>Gecikən və icrada olan etaplar</h2>
        </div>
        <Table rowKey="id" columns={columns} dataSource={stageRows.filter((stage) => ['Delayed', 'InProgress', 'Paused'].includes(stage.status))} pagination={{ pageSize: 5 }} />
      </section>
    </div>
  )
}
