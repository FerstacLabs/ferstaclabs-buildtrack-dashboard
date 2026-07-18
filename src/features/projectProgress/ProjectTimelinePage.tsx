import { CalendarOutlined, ClockCircleOutlined, DollarCircleOutlined } from '@ant-design/icons'
import { Progress, Tag } from 'antd'
import { KpiCard } from '../../components/ui/KpiCard'
import { PageTitle } from '../../components/ui/PageTitle'
import { formatHours, formatNumber } from '../../utils/formatters'
import { calculateProjectMetrics, calculateStageProgress, statusColor, statusLabel, useProjectProgressStore } from './projectProgressStore'

const formatAzn = (value: number) => `${formatNumber(value, value % 1 === 0 ? 0 : 2)} AZN`

export const ProjectTimelinePage = () => {
  const data = useProjectProgressStore()
  const metrics = calculateProjectMetrics(data)
  const crewNameById = new Map(data.crews.map((crew) => [crew.id, crew.name]))
  const stages = data.stages.slice().sort((a, b) => a.order - b.order)

  return (
    <div className="page-stack project-progress-page">
      <PageTitle title="Təqvim / Gedişat" subtitle="Villa tikintisi üzrə etapların plan və faktiki gedişat xəritəsi" />

      <section className="kpi-grid four">
        <KpiCard icon={<CalendarOutlined />} title="Etap sayı" value={formatNumber(stages.length)} tone="blue" />
        <KpiCard icon={<ClockCircleOutlined />} title="Plan saat" value={formatHours(metrics.plannedHours, 0)} tone="green" />
        <KpiCard icon={<ClockCircleOutlined />} title="Faktiki saat" value={formatHours(metrics.actualHours, 0)} tone="orange" />
        <KpiCard icon={<DollarCircleOutlined />} title="Smeta dəyəri" value={formatAzn(data.summary.totalAmount)} tone="purple" />
      </section>

      <section className="table-card">
        <div className="card-heading">
          <h2>Etap timeline</h2>
        </div>
        <div className="timeline-list">
          {stages.map((stage) => {
            const progress = calculateStageProgress(stage, data.workItems)
            return (
              <div className="project-timeline-item" key={stage.id}>
                <div className="project-timeline-marker">{stage.order}</div>
                <div className="timeline-content">
                  <div className="project-timeline-title-row">
                    <div>
                      <h3>{stage.name}</h3>
                      <span>{stage.plannedStartDate} - {stage.plannedEndDate}</span>
                    </div>
                    <Tag color={statusColor[stage.status]}>{statusLabel[stage.status]}</Tag>
                  </div>
                  <Progress percent={progress} />
                  <div className="project-timeline-meta">
                    <span>Briqada: {stage.assignedCrewId ? crewNameById.get(stage.assignedCrewId) : 'Təyin edilməyib'}</span>
                    <span>Plan/Fakt: {formatHours(stage.plannedHours, 0)} / {formatHours(stage.actualHours, 0)}</span>
                    <span>Məbləğ: {formatAzn(stage.totalCost)}</span>
                  </div>
                  {stage.notes ? <p className="project-timeline-note">{stage.notes}</p> : null}
                </div>
              </div>
            )
          })}
        </div>
      </section>
    </div>
  )
}
