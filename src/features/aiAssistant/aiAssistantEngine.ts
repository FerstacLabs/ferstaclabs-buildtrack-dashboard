import type { getAiContextSummary } from '../projectProgress/projectSelectors'
import { formatCurrency, formatHours, formatNumber, formatPercent } from '../../utils/formatters'

type AiContext = ReturnType<typeof getAiContextSummary>

export interface AssistantAnswer {
  answer: string
  confidence: number
  relatedEntities?: string[]
  suggestedQuestions?: string[]
}

const suggestions = [
  'Layihənin ümumi gedişatı neçə faizdir?',
  'Monolit briqadasının vəziyyəti necədir?',
  'Hansı işlər gecikir?',
  'Bu ay maaş xərci nə qədərdir?',
  'Kim riskli işçidir?',
  'Prorab son nə qeyd edib?',
]

const azMap: Record<string, string> = {
  ə: 'e',
  ı: 'i',
  ö: 'o',
  ü: 'u',
  ğ: 'g',
  ş: 's',
  ç: 'c',
}

export const normalizeText = (text: string) =>
  text
    .toLocaleLowerCase('az-AZ')
    .replace(/[əıöüğşç]/g, (char) => azMap[char] ?? char)
    .replace(/[^\w\s-]/g, ' ')
    .replace(/\s+/g, ' ')
    .trim()

const includesAny = (query: string, words: string[]) => words.some((word) => query.includes(normalizeText(word)))

const scoreName = (query: string, name: string) => {
  const normalizedName = normalizeText(name)
  if (query.includes(normalizedName)) return 10
  return normalizedName.split(' ').filter((part) => part.length > 2 && query.includes(part)).length
}

export const findWorkerByQuery = (query: string, workers: AiContext['workers']) =>
  workers.map((worker) => ({ worker, score: scoreName(query, worker.workerName) }))
    .filter((item) => item.score > 0)
    .sort((a, b) => b.score - a.score)[0]?.worker

export const findCrewByQuery = (query: string, crews: AiContext['crews']) =>
  crews.map((crew) => ({ crew, score: scoreName(query, crew.name) || scoreName(query, crew.type) }))
    .filter((item) => item.score > 0)
    .sort((a, b) => b.score - a.score)[0]?.crew

export const findStageByQuery = (query: string, stages: AiContext['stages']) =>
  stages.map((stage) => ({ stage, score: scoreName(query, stage.name) }))
    .filter((item) => item.score > 0)
    .sort((a, b) => b.score - a.score)[0]?.stage

export const findMaterialByQuery = (query: string, materials: AiContext['materials']) =>
  materials.map((material) => ({ material, score: scoreName(query, material.name) }))
    .filter((item) => item.score > 0)
    .sort((a, b) => b.score - a.score)[0]?.material

export const answerProjectSummary = (context: AiContext): AssistantAnswer => {
  const delayed = context.stages.filter((stage) => stage.status === 'Delayed')
  return {
    confidence: 0.95,
    relatedEntities: [context.project.name],
    answer: `${context.project.name} üzrə ümumi gedişat ${formatPercent(context.metrics.weightedProgress, 1)}-dir. Yekun smeta ${formatCurrency(context.summary.totalAmount)} təşkil edir: işçilik ${formatCurrency(context.summary.laborAmount)}, material ${formatCurrency(context.summary.materialAmount)}, gizli xərclər ${formatCurrency(context.summary.hiddenCostAmount)}. Plan saat ${formatHours(context.metrics.plannedHours, 0)}, faktiki saat ${formatHours(context.metrics.actualHours, 0)}, qalan saat ${formatHours(context.metrics.remainingHours, 0)}. Aktiv briqada sayı ${formatNumber(context.metrics.activeCrews)}, gecikən etap sayı ${formatNumber(delayed.length)}.`,
  }
}

export const answerWorkerHours = (context: AiContext, query: string): AssistantAnswer => {
  const worker = findWorkerByQuery(query, context.workers)
  if (worker) {
    const payroll = context.payrollRows.find((row) => row.workerId === worker.id)
    return {
      confidence: 0.92,
      relatedEntities: [worker.workerName],
      answer: `${worker.workerName} üçün cari hesab dövründə təsdiqli saat ${formatHours(payroll?.approvedHours ?? 0, 1)}-dır. Briqada: ${payroll?.crewName ?? 'Təyin edilməyib'}, rol: ${worker.role}, saatlıq tarif: ${formatCurrency(worker.hourlyRate)}.`,
    }
  }

  const sorted = context.payrollRows.slice().sort((a, b) => b.approvedHours - a.approvedHours)
  const selected = includesAny(query, ['az', 'az saat', 'kim az']) ? sorted.slice(-5).reverse() : sorted.slice(0, 5)
  return {
    confidence: 0.85,
    relatedEntities: selected.map((row) => row.workerName),
    answer: selected.map((row) => `${row.workerName}: ${formatHours(row.approvedHours, 1)} (${row.crewName})`).join('\n'),
  }
}

export const answerCrewStatus = (context: AiContext, query: string): AssistantAnswer => {
  const crew = findCrewByQuery(query, context.crews)
  const delayedCrews = context.crews.filter((item) => item.status === 'Delayed' || item.progressPercent && item.progressPercent < 25)
  if (!crew) {
    return {
      confidence: 0.82,
      relatedEntities: delayedCrews.map((item) => item.name),
      answer: delayedCrews.length
        ? `Gecikmə riski olan briqadalar: ${delayedCrews.map((item) => `${item.name} (${formatPercent(item.progressPercent ?? 0, 1)})`).join(', ')}.`
        : 'Briqadalar üzrə kritik gecikmə görünmür.',
    }
  }
  const stage = context.stages.find((item) => item.id === crew.activeWorkStageId)
  const workItem = context.workItems.find((item) => item.id === crew.activeWorkItemId)
  return {
    confidence: 0.94,
    relatedEntities: [crew.name, stage?.name ?? '', workItem?.name ?? ''].filter(Boolean),
    answer: `${crew.name}: prorab ${crew.foremanName}, işçi sayı ${formatNumber(crew.workerCount)}, aktiv iş "${workItem?.name ?? stage?.name ?? 'təyin edilməyib'}". Plan saat ${formatHours(crew.plannedDailyHours, 0)}/gün, faktiki saat ${formatHours(crew.actualHours ?? 0, 1)}, gedişat ${formatPercent(crew.progressPercent ?? stage?.calculatedProgress ?? 0, 1)}.`,
  }
}

export const answerStageStatus = (context: AiContext, query: string): AssistantAnswer => {
  const stage = findStageByQuery(query, context.stages)
  if (stage) {
    const items = context.workItems.filter((item) => item.stageId === stage.id)
    return {
      confidence: 0.93,
      relatedEntities: [stage.name, ...items.slice(0, 3).map((item) => item.name)],
      answer: `${stage.name}: status "${stage.status}", gedişat ${formatPercent(stage.calculatedProgress, 1)}, plan/fakt saat ${formatHours(stage.plannedHours, 0)} / ${formatHours(stage.actualHours, 1)}. İş sətirləri: ${items.map((item) => `${item.name} (${formatPercent(item.progressPercent, 1)})`).join(', ')}.`,
    }
  }
  const delayed = context.workItems.filter((item) => item.status === 'Delayed')
  return {
    confidence: 0.86,
    relatedEntities: delayed.map((item) => item.name),
    answer: delayed.length
      ? `Gecikən işlər: ${delayed.map((item) => `${item.name} - ${formatPercent(item.progressPercent, 1)}`).join(', ')}.`
      : `Qalan iş saatı ${formatHours(context.metrics.remainingHours, 0)}-dır. Gecikən iş sətiri görünmür.`,
  }
}

export const answerPayroll = (context: AiContext, query: string): AssistantAnswer => {
  const worker = findWorkerByQuery(query, context.workers)
  if (worker) {
    const payroll = context.payrollRows.find((row) => row.workerId === worker.id)
    return {
      confidence: 0.93,
      relatedEntities: [worker.workerName],
      answer: payroll
        ? `${worker.workerName} üçün yekun maaş ${formatCurrency(payroll.finalAmount)}-dır. Normal saat ${formatHours(payroll.normalHours, 1)}, overtime ${formatHours(payroll.overtimeHours, 1)}, tarif ${formatCurrency(payroll.hourlyRate)}/saat.`
        : `${worker.workerName} üçün payroll sətri tapılmadı.`,
    }
  }
  const total = context.payrollRows.reduce((sum, row) => sum + row.finalAmount, 0)
  const overtime = context.payrollRows.reduce((sum, row) => sum + row.overtimeHours, 0)
  return {
    confidence: 0.91,
    answer: `Cari hesab dövrü üzrə maaş xərci ${formatCurrency(total)}-dır. Təsdiqli işçi sayı ${formatNumber(context.payrollRows.length)}, toplam overtime ${formatHours(overtime, 1)}.`,
  }
}

export const answerRisks = (context: AiContext): AssistantAnswer => {
  const workerText = context.riskWorkers.slice(0, 6).map((worker) => `${worker.workerName} (${worker.riskScore})`).join(', ')
  const stageText = context.stages.filter((stage) => stage.status === 'Delayed' || stage.status === 'Paused').map((stage) => stage.name).join(', ')
  return {
    confidence: 0.89,
    relatedEntities: [...context.riskWorkers.slice(0, 6).map((worker) => worker.workerName), stageText],
    answer: `Risk xülasəsi: riskli işçilər ${workerText || 'yoxdur'}. Qrafik riski olan etaplar: ${stageText || 'yoxdur'}. Açıq risk qeydləri: ${context.risks.map((risk) => risk.title).join('; ') || 'yoxdur'}.`,
  }
}

export const answerDailyReports = (context: AiContext, query: string): AssistantAnswer => {
  const report = context.dailyReports[0]
  if (!report) {
    return { confidence: 0.75, answer: 'Prorab gündəliyi tapılmadı.', suggestedQuestions: suggestions }
  }
  if (includesAny(query, ['yagis', 'hava'])) {
    const weatherReports = context.dailyReports.filter((item) => item.weatherIssue || item.weather === 'Yağışlı')
    return {
      confidence: 0.82,
      answer: weatherReports.length
        ? `Hava təsiri olan qeydlər: ${weatherReports.map((item) => `${item.date}: ${item.weatherIssue ?? item.weather}`).join('; ')}.`
        : 'Gündəliklərdə hava səbəbli ciddi gecikmə qeyd edilməyib.',
    }
  }
  return {
    confidence: 0.9,
    relatedEntities: [report.foremanName],
    answer: `Son prorab qeydi (${report.date}, ${report.foremanName}): ${report.todayNotes}${report.delayReason ? ` Gecikmə səbəbi: ${report.delayReason}.` : ''}`,
  }
}

export const answerMaterials = (context: AiContext, query: string): AssistantAnswer => {
  const material = findMaterialByQuery(query, context.materials)
  if (material) {
    return {
      confidence: 0.93,
      relatedEntities: [material.name],
      answer: `${material.name}: plan ${formatNumber(material.quantity, 1)} ${material.unit}, istifadə ${formatNumber(material.usedQuantity, 1)} ${material.unit}, qalıq ${formatNumber(material.remainingQuantity, 1)} ${material.unit}. Təchizatçı: ${material.supplier ?? 'qeyd edilməyib'}.`,
    }
  }
  const low = context.materials.filter((item) => item.quantity > 0 && item.remainingQuantity / item.quantity <= 0.15)
  return {
    confidence: 0.86,
    relatedEntities: low.map((item) => item.name),
    answer: low.length
      ? `Azalan materiallar: ${low.map((item) => `${item.name} - ${formatNumber(item.remainingQuantity, 1)} ${item.unit}`).join(', ')}.`
      : 'Kritik səviyyədə azalan material görünmür.',
  }
}

export const getAssistantAnswer = (query: string, context: AiContext): AssistantAnswer => {
  const normalized = normalizeText(query)

  if (includesAny(normalized, ['maas', 'emek haqqi', 'odenis', 'xerc'])) return answerPayroll(context, normalized)
  if (includesAny(normalized, ['material', 'beton', 'armatur', 'taxta', 'kubik', 'qalib', 'qalıb', 'istifade olunub'])) return answerMaterials(context, normalized)
  if (includesAny(normalized, ['prorab', 'gundelik', 'qeyd', 'bugun hansi isler', 'hava', 'yagis'])) return answerDailyReports(context, normalized)
  if (includesAny(normalized, ['risk', 'gecikme sebeb', 'riskli'])) return answerRisks(context)
  if (includesAny(normalized, ['briqada', 'monolit', 'horgu', 'suvaq', 'dam', 'pencere', 'logistika'])) return answerCrewStatus(context, normalized)
  if (includesAny(normalized, ['isci', 'kim', 'saat isleyib', 'cox isleyen', 'az saat']) || findWorkerByQuery(normalized, context.workers)) return answerWorkerHours(context, normalized)
  if (includesAny(normalized, ['isler gecikir', 'etap', 'bunovre', 'qalan is', 'tamamlanib', 'ne yerdedir'])) return answerStageStatus(context, normalized)
  if (includesAny(normalized, ['layihe', 'umumi', 'gedisat', 'smeta', 'xulase', 'veziyyet'])) return answerProjectSummary(context)

  return {
    confidence: 0.35,
    answer: 'Sualı daha dəqiq yazsanız, layihə, briqada, işçi, maaş, material və ya prorab gündəliyi üzrə konkret rəqəmlərlə cavab verə bilərəm.',
    suggestedQuestions: suggestions,
  }
}
