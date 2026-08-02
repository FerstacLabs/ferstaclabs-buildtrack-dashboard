import { CalendarOutlined, ClockCircleOutlined, DollarCircleOutlined } from '@ant-design/icons'
import { Progress, Tag } from 'antd'
import { ProjectSelect } from '../../components/ProjectSelect'
import { KpiCard } from '../../components/ui/KpiCard'
import { PageTitle } from '../../components/ui/PageTitle'
import { formatCurrency, formatHours, formatNumber } from '../../utils/formatters'
import { getDashboardSummary, getStageActualHours, getStagesByObject } from './projectSelectors'
import { calculateStageProgress, statusColor, statusLabel, useProjectProgressStore } from './projectProgressStore'
import { useProjectSelectionStore } from '../../stores/projectSelectionStore'

export const ProjectTimelinePage = () => {
  const data = useProjectProgressStore()
  const selectedObjectId = useProjectSelectionStore((state) => state.selectedProjectId)
  const metrics = getDashboardSummary(data, data.project.id, selectedObjectId)
  const crewNameById = new Map(data.crews.map((crew) => [crew.id, crew.name]))
  const stages = getStagesByObject(data, selectedObjectId)
  const estimateTotal = stages.reduce((sum, stage) => sum + stage.totalCost, 0)

  return (
    <div className="page-stack project-progress-page">
      <PageTitle title="Təqvim / Gedişat" subtitle={`${data.project.name} üzrə etapların plan və faktiki gedişat xəritəsi`} extra={<ProjectSelect pageKey="timeline" />} />

      <section className="kpi-grid four">
        <KpiCard icon={<CalendarOutlined />} title="Etap sayı" value={formatNumber(stages.length)} tone="blue" />
        <KpiCard icon={<ClockCircleOutlined />} title="Plan saat" value={formatHours(metrics.plannedHours, 0)} tone="green" />
        <KpiCard icon={<ClockCircleOutlined />} title="Faktiki saat" value={formatHours(metrics.actualHours, 0)} tone="orange" />
        <KpiCard icon={<DollarCircleOutlined />} title="Smeta dəyəri" value={formatCurrency(estimateTotal)} tone="purple" />
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
                    <span>Plan/Fakt: {formatHours(stage.plannedHours, 0)} / {formatHours(getStageActualHours(data, stage.id) || stage.actualHours, 0)}</span>
                    <span>Məbləğ: {formatCurrency(stage.totalCost)}</span>
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
