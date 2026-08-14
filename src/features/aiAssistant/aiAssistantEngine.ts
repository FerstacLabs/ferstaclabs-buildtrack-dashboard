import { formatCurrency, formatHours, formatNumber, formatPercent } from '../../utils/formatters'
import type {
  AiProjectContext,
  AiRelatedEntity,
} from './aiContextBuilder'

export type AiIntent =
  | 'OVERALL_STATUS'
  | 'RISKS'
  | 'DELAYS'
  | 'FINANCE'
  | 'WORKFORCE'
  | 'PRIORITIES'
  | 'SAFETY_SECURITY'
  | 'PROACTIVE_BRIEF'
  | 'OBJECT_STATUS'
  | 'CREW_STATUS'
  | 'MATERIALS'
  | 'DAILY_REPORTS'
  | 'PAYROLL'
  | 'HELP'

export interface AiAnswer {
  answer: string
  confidence: number
  intent: AiIntent
  relatedEntities?: AiRelatedEntity[]
  suggestedQuestions?: string[]
}

const executiveSuggestions = [
  'Bugünkü ümumi vəziyyət necədir?',
  'Hazırda ən kritik risklər hansılardır?',
  'Hansı layihələr plan üzrə getmir?',
  'Büdcə vəziyyəti necədir?',
  'İşçi heyətinin vəziyyəti necədir?',
  'Bu gün ilk növbədə nəyə diqqət etməliyəm?',
  'Təhlükəsizliklə bağlı hər hansı problem varmı?',
  'Mənə vacib məlumatları özün təqdim et',
  'Monolit briqadasının vəziyyəti necədir?',
  'Hansı material azalır?',
]

const transliterationMap: Record<string, string> = {
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
    .replace(/[əıöüğşç]/g, (char) => transliterationMap[char] ?? char)
    .replace(/maas/g, 'maas')
    .replace(/maaş/g, 'maas')
    .replace(/isci/g, 'isci')
    .replace(/işçi/g, 'isci')
    .replace(/gecikmə/g, 'gecikme')
    .replace(/layihə/g, 'layihe')
    .replace(/[^\w\s-]/g, ' ')
    .replace(/\s+/g, ' ')
    .trim()

const includesAny = (query: string, words: string[]) =>
  words.some((word) => query.includes(normalizeText(word)))

const words = (value: string) => normalizeText(value).split(' ').filter((part) => part.length > 2)

const scoreName = (query: string, name: string) => {
  const normalizedName = normalizeText(name)
  if (query.includes(normalizedName)) return 20
  const nameWords = words(name)
  return nameWords.reduce((score, word) => score + (query.includes(word) ? 4 : 0), 0)
}

const asEntity = (type: string, id: string, name: string): AiRelatedEntity => ({ type, id, name })

const list = (items: string[]) => items.filter(Boolean).map((item, index) => `${index + 1}. ${item}`).join('\n')

const objectLabel = (context: AiProjectContext) =>
  context.selectedObject?.name ?? `${formatNumber(context.objects.length)} layihə`

const topBy = <T,>(items: T[], selector: (item: T) => number, count = 3) =>
  items.slice().sort((a, b) => selector(b) - selector(a)).slice(0, count)

export const findObjectInQuery = (query: string, objects: AiProjectContext['objects']) =>
  topBy(objects.map((object) => ({ object, score: scoreName(query, object.name) })), (item) => item.score, 1)
    .find((item) => item.score > 0)?.object

export const findStageInQuery = (query: string, stages: AiProjectContext['stages']) =>
  topBy(stages.map((stage) => ({ stage, score: scoreName(query, stage.name) })), (item) => item.score, 1)
    .find((item) => item.score > 0)?.stage

export const findCrewInQuery = (query: string, crews: AiProjectContext['crews']) =>
  topBy(crews.map((crew) => ({ crew, score: Math.max(scoreName(query, crew.name), scoreName(query, crew.type)) })), (item) => item.score, 1)
    .find((item) => item.score > 0)?.crew

export const findCrewsInQuery = (query: string, crews: AiProjectContext['crews']) =>
  crews
    .map((crew) => ({ crew, score: Math.max(scoreName(query, crew.name), scoreName(query, crew.type)) }))
    .filter((item) => item.score > 0)
    .sort((a, b) => b.score - a.score)
    .map((item) => item.crew)

export const findWorkerInQuery = (query: string, workers: AiProjectContext['workers']) =>
  topBy(workers.map((worker) => ({ worker, score: Math.max(scoreName(query, worker.workerName), scoreName(query, worker.workerExternalId)) })), (item) => item.score, 1)
    .find((item) => item.score > 0)?.worker

export const findMaterialInQuery = (query: string, materials: AiProjectContext['materials']) =>
  topBy(materials.map((material) => ({ material, score: scoreName(query, material.name) })), (item) => item.score, 1)
    .find((item) => item.score > 0)?.material

const detectIntent = (query: string, context: AiProjectContext): AiIntent => {
  if (includesAny(query, ['sen nə edə bilirsən', 'hansı suallar', 'kömək', 'help'])) return 'HELP'
  if (includesAny(query, ['özün analiz et', 'rəhbər üçün', 'brifinq', 'vacib məlumatları özün', 'nə bilməliyəm'])) return 'PROACTIVE_BRIEF'
  if (includesAny(query, ['prioritet', 'ilk növbədə', 'nəyi yoxlamalıyam', 'vacib işləri'])) return 'PRIORITIES'
  if (includesAny(query, ['təhlükəsizlik', 'tanınmayan', 'security', 'yad üz', 'yad sexs', 'yad şəxs'])) return 'SAFETY_SECURITY'
  if (findObjectInQuery(query, context.objects)) return 'OBJECT_STATUS'
  if (includesAny(query, ['material', 'beton', 'armatur', 'taxta', 'kubik', 'aqlay', 'qalıq', 'azalır']) || findMaterialInQuery(query, context.materials)) return 'MATERIALS'
  if (includesAny(query, ['prorab', 'gündəlik', 'son qeyd', 'hava', 'yağış', 'görülüb', 'hesabat'])) return 'DAILY_REPORTS'
  if (includesAny(query, ['maaş', 'maas', 'payroll', 'overtime', 'kimə nə qədər', 'əmək haqqı'])) return 'PAYROLL'
  if (includesAny(query, ['büdc', 'maliyy', 'smeta xərci', 'bu ay xərc', 'xərc nə qədər'])) return 'FINANCE'
  if (includesAny(query, ['risk', 'problem', 'kritik vəziyyət', 'diqqət etməliyəm'])) return 'RISKS'
  if (includesAny(query, ['gecik', 'plan üzrə getmir', 'qrafikdən geri', 'geri qalan'])) return 'DELAYS'
  if (includesAny(query, ['briqada', 'monolit', 'hörgü', 'horgu', 'suvaq', 'dam', 'pəncərə', 'pencere', 'logistika']) || findCrewInQuery(query, context.crews)) return 'CREW_STATUS'
  if (includesAny(query, ['işçi', 'isci', 'heyət', 'kim neçə saat', 'ən çox işləyən', 'neçə işçi'])) return 'WORKFORCE'
  return 'OVERALL_STATUS'
}

export const answerOverallStatus = (context: AiProjectContext): AiAnswer => {
  const summary = context.summary
  const topDelay = topBy(context.stages, (stage) => stage.status === 'Delayed' ? stage.planFactGapHours + 100 : stage.planFactGapHours, 1)[0]
  const recommendation = summary.delayedStagesCount || summary.materialCriticalCount
    ? 'Tövsiyə: gecikən etaplar və kritik material qalıqları üzrə bu gün ayrıca bərpa planı yoxlanmalıdır.'
    : 'Tövsiyə: plan/fakt saat fərqini və gündəlik prorab qeydlərini gündəlik ritmdə izləmək kifayətdir.'
  return {
    intent: 'OVERALL_STATUS',
    confidence: 0.94,
    relatedEntities: topDelay ? [asEntity('stage', topDelay.id, topDelay.name)] : undefined,
    suggestedQuestions: ['Hansı işlər gecikir?', 'Büdcə vəziyyəti necədir?', 'Bu gün nəyə baxmalıyam?'],
    answer: `Hazırda ${objectLabel(context)} üzrə vəziyyət ${summary.delayedStagesCount > 0 || summary.materialCriticalCount > 0 ? 'nəzarət tələb edir' : 'nəzarət altındadır'}.\n${list([
      `Ümumi icra göstəricisi ${formatPercent(summary.overallProgressPercent, 1)}-dir; ${formatNumber(summary.activeObjectCount)} aktiv layihə izlənir.`,
      `Plan/fakt saat: ${formatHours(summary.totalPlannedHours, 0)} / ${formatHours(summary.totalActualHours, 0)}; qalan saat ${formatHours(summary.remainingHours, 0)}.`,
      `Gecikən etap: ${formatNumber(summary.delayedStagesCount)}, dayandırılmış etap: ${formatNumber(summary.pausedStagesCount)}, riskli işçi: ${formatNumber(summary.riskWorkersCount)}.`,
      `Kritik material qalığı: ${formatNumber(summary.materialCriticalCount)}, cari payroll yükü: ${formatCurrency(summary.payrollFinalTotal)}.`,
      recommendation,
    ])}`,
  }
}

export const answerRisks = (context: AiProjectContext): AiAnswer => {
  const insights = context.topInsights.filter((insight) => ['critical', 'warning'].includes(insight.severity)).slice(0, 5)
  const derivedRisks = topBy(context.risks, (risk) => risk.riskScore, 3).map((risk) =>
    `${risk.objectName}: ${risk.workerName} üzrə ${risk.riskScore} bal risk. Mənbə: ${risk.source}; səbəb: ${risk.reason}.`,
  )
  const riskLines = insights.length
    ? insights.map((insight) => `${insight.title}. ${insight.detail}`)
    : derivedRisks
  return {
    intent: 'RISKS',
    confidence: 0.91,
    relatedEntities: insights.flatMap((insight) => insight.relatedEntities ?? []),
    suggestedQuestions: ['Bu gün nəyə diqqət etməliyəm?', 'Hansı material azalır?', 'Prorab son nə qeyd edib?'],
    answer: `Hazırda diqqət tələb edən əsas risklər bunlardır:\n${list(riskLines.length ? riskLines : ['Kritik risk görünmür, amma plan/fakt saat və material qalıqları gündəlik izlənməlidir.'])}\nTövsiyə: riskləri material, gecikmə və prorab qeydləri üzrə ayrı-ayrı məsul şəxsə bağlayın.`,
  }
}

export const answerDelays = (context: AiProjectContext): AiAnswer => {
  const delayedStages = context.stages
    .filter((stage) => stage.status === 'Delayed' || stage.planFactGapHours > 50)
    .sort((a, b) => b.planFactGapHours - a.planFactGapHours)
    .slice(0, 5)
  const reportReasons = context.dailyReports.filter((report) => report.delayReason).slice(0, 3)
  return {
    intent: 'DELAYS',
    confidence: 0.9,
    relatedEntities: delayedStages.map((stage) => asEntity('stage', stage.id, stage.name)),
    suggestedQuestions: ['Hansı briqada gecikir?', 'Bərpa üçün prioritet nədir?', 'Material çatışmazlığı var?'],
    answer: delayedStages.length
      ? `Plan üzrə getməyən əsas işlər:\n${list(delayedStages.map((stage) => `${stage.objectName} - ${stage.name}: icra ${formatPercent(stage.calculatedProgress, 1)}, plan/fakt fərqi ${formatHours(Math.max(0, stage.planFactGapHours), 0)}.`))}\n${reportReasons.length ? `Prorab qeydlərində səbəblər: ${reportReasons.map((report) => `${report.objectName}: ${report.delayReason}`).join('; ')}.\n` : ''}Tövsiyə: ən böyük saat fərqi olan etaplara əlavə briqada saatı və material təminatı ayırın.`
      : `Seçilən kontekstdə kritik gecikmə görünmür. Qalan saat ${formatHours(context.summary.remainingHours, 0)}-dır; plan/fakt fərqi olan etapları gündəlik izləmək tövsiyə olunur.`,
  }
}

export const answerFinance = (context: AiProjectContext): AiAnswer => {
  const summary = context.summary
  const budgetUsage = summary.totalSmetaAmount ? (summary.payrollFinalTotal / summary.totalSmetaAmount) * 100 : 0
  const costDrivers = [
    ...topBy(context.materials, (material) => material.totalValue, 2).map((material) => `${material.name}: ${formatCurrency(material.totalValue)}`),
    ...topBy(context.payroll, (row) => row.finalAmount, 2).map((row) => `${row.workerName}: ${formatCurrency(row.finalAmount)}`),
  ].slice(0, 4)
  return {
    intent: 'FINANCE',
    confidence: 0.9,
    relatedEntities: topBy(context.materials, (material) => material.totalValue, 2).map((material) => asEntity('material', material.id, material.name)),
    suggestedQuestions: ['Bu ay maaş xərci nə qədərdir?', 'Hansı material ən bahalıdır?', 'Export hazırdır?'],
    answer: `Maliyyə vəziyyəti üzrə qısa nəticə: ${objectLabel(context)} üçün smeta və payroll yükü izlənə bilir.\n${list([
      `Yekun smeta: ${formatCurrency(summary.totalSmetaAmount)}; işçilik büdcəsi ${formatCurrency(summary.totalLaborBudget)}, material büdcəsi ${formatCurrency(summary.totalMaterialBudget)}.`,
      `Gözə görünməyən xərclər: ${formatCurrency(summary.totalHiddenCost)}.`,
      `Cari payroll gross: ${formatCurrency(summary.payrollGrossTotal)}, yekun payroll: ${formatCurrency(summary.payrollFinalTotal)}.`,
      `Payroll-un smetaya nisbəti təxminən ${formatPercent(budgetUsage, 1)}-dir.`,
      `Əsas xərc drayverləri: ${costDrivers.join('; ') || 'kifayət qədər xərc datası yoxdur'}.`,
    ])}\nTövsiyə: payroll/export təsdiqini material qalıqları ilə birlikdə yoxlayın.`,
  }
}

export const answerWorkforce = (context: AiProjectContext, query: string): AiAnswer => {
  const worker = findWorkerInQuery(query, context.workers)
  if (worker) {
    return {
      intent: 'WORKFORCE',
      confidence: 0.94,
      relatedEntities: [asEntity('worker', worker.id, worker.workerName)],
      suggestedQuestions: ['Bu işçinin maaşı nə qədərdir?', 'Hansı briqada gecikir?'],
      answer: `${worker.workerName} üzrə vəziyyət:\n${list([
        `Layihə: ${worker.objectName}; briqada: ${worker.crewName}; rol: ${worker.role}.`,
        `Təsdiqli saat: ${formatHours(worker.approvedHours, 1)}, overtime: ${formatHours(worker.overtimeHours, 1)}.`,
        `Cari payroll məbləği: ${formatCurrency(worker.payrollAmount)}; risk balı: ${formatNumber(worker.riskScore)}.`,
        worker.riskScore >= 60 ? 'Tövsiyə: davamiyyət və prorab təsdiqləri ayrıca yoxlanmalıdır.' : 'Tövsiyə: cari göstəricilər normal görünür.',
      ])}`,
    }
  }

  const topCrews = topBy(context.crews, (crew) => crew.actualHoursDerived, 4)
  const riskCrews = context.crews
    .filter((crew) => crew.planFactGapHours > 40 || crew.status === 'Delayed')
    .slice(0, 3)
  const topWorkers = topBy(context.workers, (item) => item.approvedHours, 5)
  const asksTopWorkers = includesAny(query, ['ən çox', 'cox isleyen', 'kim neçə saat', 'kim nece saat'])
  return {
    intent: 'WORKFORCE',
    confidence: 0.9,
    relatedEntities: topCrews.map((crew) => asEntity('crew', crew.id, crew.name)),
    suggestedQuestions: ['Monolit briqadasının vəziyyəti necədir?', 'Kim riskli işçidir?', 'Bu ay payroll nə qədərdir?'],
    answer: asksTopWorkers
      ? `Ən çox saat işləyən işçilər:\n${list(topWorkers.map((worker) => `${worker.workerName} (${worker.objectName}): ${formatHours(worker.approvedHours, 1)}, payroll ${formatCurrency(worker.payrollAmount)}.`))}`
      : `İşçi heyəti üzrə ümumi vəziyyət:\n${list([
        `Cəmi işçi: ${formatNumber(context.summary.totalWorkersCount)}, aktiv işçi: ${formatNumber(context.summary.activeWorkersCount)}, davamiyyət göstəricisi ${formatPercent(context.summary.attendancePercent, 1)}.`,
        `Aktiv briqada sayı: ${formatNumber(context.summary.activeCrewsCount)}.`,
        `Ən çox faktiki saat toplayan briqadalar: ${topCrews.map((crew) => `${crew.name} - ${formatHours(crew.actualHoursDerived, 0)}`).join('; ')}.`,
        riskCrews.length ? `Nəzarət tələb edən briqadalar: ${riskCrews.map((crew) => `${crew.name} (${crew.objectName})`).join(', ')}.` : 'Briqadalar üzrə kritik resurs çatışmazlığı görünmür.',
      ])}`,
  }
}

export const answerPriorities = (context: AiProjectContext): AiAnswer => {
  const delayed = topBy(context.stages.filter((stage) => stage.status === 'Delayed' || stage.planFactGapHours > 50), (stage) => stage.planFactGapHours, 1)[0]
  const material = topBy(context.materials.filter((item) => item.isCritical), (item) => 100 - item.remainingPercent, 1)[0]
  const crewGap = topBy(context.crews, (crew) => crew.planFactGapHours, 1)[0]
  const report = context.dailyReports.find((item) => item.openIssueCount > 0)
  const priorityLines = [
    delayed ? `${delayed.objectName} üzrə ${delayed.name} etapının plan/fakt fərqini bağlayın.` : 'Gecikmə olmayan etaplarda plan saatlarını gündəlik təsdiqləyin.',
    material ? `${material.name} qalığını yoxlayın: ${formatNumber(material.remainingQuantity, 1)} ${material.unit} qalıb.` : 'Material ehtiyatlarında kritik azalma görünmür, yenə də əsas beton/armatur qalıqlarını yoxlayın.',
    crewGap ? `${crewGap.name} üçün faktiki saat bölgüsünü yoxlayın: fərq ${formatHours(Math.max(0, crewGap.planFactGapHours), 0)}.` : 'Briqada saat bölgüsü normal görünür.',
    report ? `${report.foremanName} prorabın son açıq qeydini bağlayın: ${report.delayReason ?? report.materialShortage ?? report.equipmentIssue ?? report.weatherIssue}.` : 'Prorab gündəliklərində açıq problem qeydini təsdiqləyin.',
    context.summary.exportWarningCount ? `Payroll/export üzrə ${formatNumber(context.summary.exportWarningCount)} xəbərdarlıq sətirini yoxlayın.` : 'Payroll/export təsdiq statusunu gün sonunda yeniləyin.',
  ]
  return {
    intent: 'PRIORITIES',
    confidence: 0.93,
    suggestedQuestions: ['Bu prioritetlər üzrə risk nədir?', 'Hansı material azalır?', 'Prorab son nə qeyd edib?'],
    answer: `Bu gün ilk növbədə bunlara diqqət etməyiniz yaxşı olar:\n${list(priorityLines)}`,
  }
}

export const answerSafetySecurity = (context: AiProjectContext): AiAnswer => {
  const auditWarnings = context.audit.filter((row) => !normalizeText(String(row.auditStatus)).includes('uygun'))
  const safetyReports = context.dailyReports.filter((report) => report.equipmentIssue || report.weatherIssue || report.delayReason)
  return {
    intent: 'SAFETY_SECURITY',
    confidence: 0.86,
    relatedEntities: topBy(context.risks, (risk) => risk.riskScore, 3).map((risk) => asEntity('worker', risk.id, risk.workerName)),
    suggestedQuestions: ['Hazırda ən kritik risklər hansılardır?', 'Prorab son nə qeyd edib?', 'Riskli işçi var?'],
    answer: `Təhlükəsizlik və nəzarət baxımından əsas göstəricilər:\n${list([
      `Riskli işçi sayı: ${formatNumber(context.summary.riskWorkersCount)}.`,
      `Audit xəbərdarlığı olan prorab/crew sətirləri: ${formatNumber(auditWarnings.length)}.`,
      `Gündəlik hesabatlarda açıq təhlükə/problem qeydləri: ${formatNumber(safetyReports.length)}.`,
      context.risks[0] ? `Ən yüksək risk: ${context.risks[0].workerName} (${context.risks[0].objectName}) - ${context.risks[0].riskScore} bal.` : 'Kritik worker riski görünmür.',
      'Tövsiyə: riskli worker-lər, prorab gec təsdiqləri və sahə qeydləri üzrə məsul şəxs təyin edin.',
    ])}`,
  }
}

export const answerProactiveBrief = (context: AiProjectContext): AiAnswer => {
  const delayed = topBy(context.stages.filter((stage) => stage.status === 'Delayed' || stage.planFactGapHours > 50), (stage) => stage.planFactGapHours, 1)[0]
  const material = topBy(context.materials.filter((item) => item.isCritical), (item) => 100 - item.remainingPercent, 1)[0]
  const crewGap = topBy(context.crews, (crew) => crew.planFactGapHours, 1)[0]
  const report = context.dailyReports.find((item) => item.openIssueCount > 0)
  const payrollIssue = context.summary.exportWarningCount || context.summary.overtimeHours > 0
  const brief = [
    delayed ? `Ən böyük gecikmə: ${delayed.objectName} - ${delayed.name}, plan/fakt fərqi ${formatHours(Math.max(0, delayed.planFactGapHours), 0)}.` : `Gecikmə bloku: kritik etap gecikməsi görünmür.`,
    material ? `Material nəzarəti: ${material.name} ${material.objectName} üzrə ${formatNumber(material.remainingQuantity, 1)} ${material.unit} qalıb.` : 'Material nəzarəti: kritik qalıq yoxdur.',
    crewGap ? `Saat fərqi: ${crewGap.name} (${crewGap.objectName}) üzrə ${formatHours(Math.max(0, crewGap.planFactGapHours), 0)} fərq var.` : 'Saat bölgüsü: böyük plan/fakt fərqi görünmür.',
    payrollIssue ? `Payroll/export: ${formatCurrency(context.summary.payrollFinalTotal)} yekun payroll, ${formatHours(context.summary.overtimeHours, 1)} overtime izlənir.` : 'Payroll/export: kritik xəbərdarlıq görünmür.',
    report ? `Prorab qeydi: ${report.objectName} üzrə ${report.foremanName} açıq məsələ qeyd edib.` : 'Prorab gündəlikləri: açıq problem qeydi görünmür.',
  ]
  return {
    intent: 'PROACTIVE_BRIEF',
    confidence: 0.95,
    suggestedQuestions: ['Bu məsələlər üzrə detal ver', 'Hansı layihələr plan üzrə getmir?', 'Büdcə vəziyyəti necədir?'],
    answer: `Bugünkü vəziyyətə əsasən diqqət tələb edən əsas məsələlər bunlardır:\n${list(brief)}\nİstəsəniz, bu məsələlər üzrə ayrıca detallı hesabat da çıxara bilərəm.`,
  }
}

export const answerObjectStatus = (context: AiProjectContext, objectName?: string): AiAnswer => {
  const object = objectName
    ? context.objects.find((item) => normalizeText(item.name) === normalizeText(objectName)) ?? findObjectInQuery(normalizeText(objectName), context.objects)
    : context.selectedObject
  const objectId = object?.id
  const stages = objectId ? context.stages.filter((stage) => stage.objectId === objectId) : context.stages
  const crews = objectId ? context.crews.filter((crew) => crew.objectId === objectId) : context.crews
  const materials = objectId ? context.materials.filter((material) => material.objectId === objectId) : context.materials
  const reports = objectId ? context.dailyReports.filter((report) => report.objectId === objectId) : context.dailyReports
  const total = stages.reduce((sum, stage) => sum + stage.totalCost, 0)
  const progress = total ? stages.reduce((sum, stage) => sum + stage.totalCost * stage.calculatedProgress, 0) / total : context.summary.overallProgressPercent
  const plannedHours = stages.reduce((sum, stage) => sum + stage.plannedHours, 0)
  const actualHours = stages.reduce((sum, stage) => sum + stage.actualHoursDerived, 0)
  const delayed = stages.filter((stage) => stage.status === 'Delayed')
  const criticalMaterials = materials.filter((material) => material.isCritical)
  const report = reports[0]
  return {
    intent: 'OBJECT_STATUS',
    confidence: object ? 0.94 : 0.78,
    relatedEntities: object ? [asEntity('object', object.id, object.name)] : undefined,
    suggestedQuestions: ['Bu layihə üzrə hansı etap gecikir?', 'Bu layihəsin briqadaları necədir?', 'Material riski var?'],
    answer: `${object?.name ?? objectLabel(context)} üzrə vəziyyət:\n${list([
      `Smeta məbləği: ${formatCurrency(total)}, icra göstəricisi ${formatPercent(progress, 1)}.`,
      `Plan/fakt saat: ${formatHours(plannedHours, 0)} / ${formatHours(actualHours, 0)}.`,
      `Aktiv briqada: ${formatNumber(crews.length)}, gecikən etap: ${formatNumber(delayed.length)}.`,
      criticalMaterials.length ? `Material riski: ${criticalMaterials.map((material) => `${material.name} (${formatNumber(material.remainingQuantity, 1)} ${material.unit})`).join(', ')}.` : 'Kritik material riski görünmür.',
      report ? `Son prorab qeydi (${report.date}, ${report.foremanName}): ${report.todayNotes}.` : 'Son prorab hesabatı daxil edilməyib.',
      delayed.length ? 'Tövsiyə: gecikən etap üçün bərpa saatı və material təminatını ayrıca yoxlayın.' : 'Tövsiyə: cari temp saxlanılsa, plan nəzarəti normal görünür.',
    ])}`,
  }
}

export const answerCrewStatus = (context: AiProjectContext, query: string): AiAnswer => {
  const matches = findCrewsInQuery(query, context.crews)
  const crew = matches[0]
  if (!crew) {
    const delayedCrews = context.crews.filter((item) => item.status === 'Delayed' || item.planFactGapHours > 40).slice(0, 5)
    return {
      intent: 'CREW_STATUS',
      confidence: 0.78,
      relatedEntities: delayedCrews.map((item) => asEntity('crew', item.id, item.name)),
      suggestedQuestions: ['Monolit briqadasının vəziyyəti necədir?', 'Hansı briqada gecikir?'],
      answer: delayedCrews.length
        ? `Nəzarət tələb edən briqadalar:\n${list(delayedCrews.map((item) => `${item.objectName} - ${item.name}: faktiki saat ${formatHours(item.actualHoursDerived, 0)}, fərq ${formatHours(Math.max(0, item.planFactGapHours), 0)}.`))}`
        : 'Briqadalar üzrə kritik gecikmə görünmür. Konkret briqada adı yazsanız, ayrıca analiz verə bilərəm.',
    }
  }

  const relatedMatches = matches.length > 1 && !context.selectedObject ? matches.slice(0, 5) : [crew]
  if (relatedMatches.length > 1) {
    return {
      intent: 'CREW_STATUS',
      confidence: 0.88,
      relatedEntities: relatedMatches.map((item) => asEntity('crew', item.id, item.name)),
      suggestedQuestions: [`${crew.objectName} üzrə ${crew.name} necədir?`, 'Hansı briqada gecikir?'],
      answer: `Sorğuda bir neçə uyğun briqada tapıldı. Ümumi Monolit/Hörgü tipli xülasə:\n${list(relatedMatches.map((item) => `${item.objectName}: ${item.name}, prorab ${item.foremanName}, işçi ${formatNumber(item.workerCount)}, gedişat ${formatPercent(item.progressPercent ?? 0, 1)}, plan/fakt saat fərqi ${formatHours(Math.max(0, item.planFactGapHours), 0)}.`))}\nTövsiyə: konkret layihə adı ilə soruşsanız, daha dəqiq qərar xülasəsi verə bilərəm.`,
    }
  }

  return {
    intent: 'CREW_STATUS',
    confidence: 0.94,
    relatedEntities: [asEntity('crew', crew.id, crew.name)],
    suggestedQuestions: ['Bu briqadanın işçiləri kimlərdir?', 'Bu briqadada risk varmı?', 'Aktiv iş hansıdır?'],
    answer: `${crew.name} üzrə vəziyyət:\n${list([
      `Layihə: ${crew.objectName}; prorab: ${crew.foremanName}; işçi sayı: ${formatNumber(crew.workerCount)}.`,
      `Aktiv iş: ${crew.activeWorkItemName ?? crew.activeStageName ?? 'təyin edilməyib'}.`,
      `Gedişat: ${formatPercent(crew.progressPercent ?? 0, 1)}; plan/fakt saat: ${formatHours(crew.plannedHours, 0)} / ${formatHours(crew.actualHoursDerived, 0)}.`,
      crew.planFactGapHours > 40 ? `Risk: plan/fakt fərqi ${formatHours(crew.planFactGapHours, 0)}-dır.` : 'Risk: ciddi saat fərqi görünmür.',
      crew.planFactGapHours > 40 ? 'Tövsiyə: bu briqada üçün əlavə iş saatı və tapşırıq bölgüsü yoxlanmalıdır.' : 'Tövsiyə: cari ritmi qoruyun və gündəlik təsdiqi yeniləyin.',
    ])}`,
  }
}

export const answerMaterials = (context: AiProjectContext, query: string): AiAnswer => {
  const material = findMaterialInQuery(query, context.materials)
  if (material) {
    return {
      intent: 'MATERIALS',
      confidence: 0.94,
      relatedEntities: [asEntity('material', material.id, material.name)],
      suggestedQuestions: ['Hansı material azalır?', 'Bu material hansı etapla bağlıdır?'],
      answer: `${material.name} üzrə vəziyyət:\n${list([
        `Layihə: ${material.objectName}; bağlı etap: ${material.stageName ?? 'qeyd edilməyib'}.`,
        `Plan miqdar: ${formatNumber(material.quantity, 1)} ${material.unit}; istifadə: ${formatNumber(material.usedQuantity, 1)} ${material.unit} (${formatPercent(material.usedPercent, 1)}).`,
        `Qalıq: ${formatNumber(material.remainingQuantity, 1)} ${material.unit} (${formatPercent(material.remainingPercent, 1)}).`,
        `Təchizatçı: ${material.supplier ?? 'qeyd edilməyib'}; təxmini dəyər: ${formatCurrency(material.totalValue)}.`,
        material.isCritical ? 'Tövsiyə: təcili təchizat və alternativ supplier yoxlanmalıdır.' : 'Tövsiyə: qalıq normaldır, amma istifadə tempini izləmək lazımdır.',
      ])}`,
    }
  }

  const critical = context.materials.filter((item) => item.isCritical).sort((a, b) => a.remainingPercent - b.remainingPercent).slice(0, 6)
  return {
    intent: 'MATERIALS',
    confidence: 0.88,
    relatedEntities: critical.map((item) => asEntity('material', item.id, item.name)),
    suggestedQuestions: ['Beton nə qədər qalıb?', 'Armatur vəziyyəti necədir?'],
    answer: critical.length
      ? `Azalan və kritik materiallar:\n${list(critical.map((item) => `${item.objectName} - ${item.name}: ${formatNumber(item.remainingQuantity, 1)} ${item.unit} qalıb (${formatPercent(item.remainingPercent, 1)}), etap: ${item.stageName ?? 'qeyd edilməyib'}.`))}\nTövsiyə: bu materiallar üzrə satınalma və sahə istifadəsi eyni gündə yoxlanmalıdır.`
      : 'Kritik material çatışmazlığı görünmür. Əsas materiallar üzrə qalıq səviyyəsi plan daxilindədir.',
  }
}

export const answerDailyReports = (context: AiProjectContext): AiAnswer => {
  const reports = context.dailyReports.slice(0, 4)
  if (!reports.length) {
    return {
      intent: 'DAILY_REPORTS',
      confidence: 0.72,
      suggestedQuestions: executiveSuggestions,
      answer: 'Prorab gündəlikləri hələ daxil edilməyib. Mövcud smeta, briqada, saat və material datası əsasında status verə bilərəm; gündəlik hesabat əlavə olunsa, səbəb və sahə qeydləri də analizə düşəcək.',
    }
  }
  return {
    intent: 'DAILY_REPORTS',
    confidence: 0.9,
    relatedEntities: reports.map((report) => asEntity('dailyReport', report.id, report.foremanName)),
    suggestedQuestions: ['Yağış işə təsir edib?', 'Hansı işlər gecikir?', 'Bu gün prioritet nədir?'],
    answer: `Son prorab gündəlikləri:\n${list(reports.map((report) => `${report.date} - ${report.objectName}, ${report.foremanName}: ${report.todayNotes}. Hava: ${report.weather}; foto: ${formatNumber(report.photoCount)}; açıq qeyd: ${report.openIssueCount ? report.delayReason ?? report.materialShortage ?? report.equipmentIssue ?? report.weatherIssue : 'yoxdur'}.`))}\nTövsiyə: açıq qeydi olan hesabatları bu gün bağlayın və statusu təsdiqləyin.`,
  }
}

export const answerPayroll = (context: AiProjectContext, query: string): AiAnswer => {
  const worker = findWorkerInQuery(query, context.workers)
  if (worker) {
    return {
      intent: 'PAYROLL',
      confidence: 0.94,
      relatedEntities: [asEntity('worker', worker.id, worker.workerName)],
      suggestedQuestions: ['Bu işçinin saatları necədir?', 'Export hazırdır?'],
      answer: `${worker.workerName} üçün maaş hesabı:\n${list([
        `Layihə: ${worker.objectName}; briqada: ${worker.crewName}; rol: ${worker.role}.`,
        `Təsdiqli saat: ${formatHours(worker.approvedHours, 1)}, overtime: ${formatHours(worker.overtimeHours, 1)}.`,
        `Saatlıq tarif: ${formatCurrency(worker.hourlyRate)}; yekun məbləğ: ${formatCurrency(worker.payrollAmount)}.`,
        worker.riskScore >= 60 ? 'Tövsiyə: risk balı yüksək olduğu üçün təsdiqli saatlar ayrıca yoxlanmalıdır.' : 'Tövsiyə: payroll sətiri normal görünür.',
      ])}`,
    }
  }

  const topPayable = topBy(context.workers, (workerItem) => workerItem.payrollAmount, 5)
  return {
    intent: 'PAYROLL',
    confidence: 0.91,
    relatedEntities: topPayable.map((workerItem) => asEntity('worker', workerItem.id, workerItem.workerName)),
    suggestedQuestions: ['Kimə nə qədər maaş düşür?', 'Overtime nə qədərdir?', 'Export hazırdır?'],
    answer: `Payroll üzrə vəziyyət:\n${list([
      `Yekun payroll məbləği: ${formatCurrency(context.summary.payrollFinalTotal)}; gross məbləğ: ${formatCurrency(context.summary.payrollGrossTotal)}.`,
      `Təsdiqli işçi sayı: ${formatNumber(context.payroll.length)}; overtime: ${formatHours(context.summary.overtimeHours, 1)}.`,
      `Export hazır sətir: ${formatNumber(context.summary.exportReadyCount)}; xəbərdarlıq: ${formatNumber(context.summary.exportWarningCount)}.`,
      `Ən böyük ödənişlər: ${topPayable.map((item) => `${item.workerName} - ${formatCurrency(item.payrollAmount)}`).join('; ')}.`,
    ])}`,
  }
}

export const answerHelp = (): AiAnswer => ({
  intent: 'HELP',
  confidence: 0.98,
  suggestedQuestions: executiveSuggestions,
  answer: `Mən BuildTrack üzrə rəhbər köməkçisi kimi bu mövzularda cavab verə bilirəm:\n${list([
    'Ümumi layihə və layihə statusu.',
    'Gecikən etaplar, riskli işçilər və prorab qeydləri.',
    'Büdcə, smeta, payroll və export vəziyyəti.',
    'Briqada, işçi saatları və plan/fakt fərqləri.',
    'Material qalıqları və kritik təchizat xəbərdarlıqları.',
  ])}\nMəsələn: “Hazırda ən kritik risklər hansılardır?” və ya “Yasamal City Towers necə gedir?”`,
})

export const getAssistantAnswer = (query: string, context: AiProjectContext): AiAnswer => {
  const normalized = normalizeText(query)
  const intent = detectIntent(normalized, context)
  const object = findObjectInQuery(normalized, context.objects)

  switch (intent) {
    case 'HELP':
      return answerHelp()
    case 'PROACTIVE_BRIEF':
      return answerProactiveBrief(context)
    case 'PRIORITIES':
      return answerPriorities(context)
    case 'SAFETY_SECURITY':
      return answerSafetySecurity(context)
    case 'OBJECT_STATUS':
      return answerObjectStatus(context, object?.name)
    case 'CREW_STATUS':
      return answerCrewStatus(context, normalized)
    case 'MATERIALS':
      return answerMaterials(context, normalized)
    case 'DAILY_REPORTS':
      return answerDailyReports(context)
    case 'PAYROLL':
      return answerPayroll(context, normalized)
    case 'FINANCE':
      return answerFinance(context)
    case 'WORKFORCE':
      return answerWorkforce(context, normalized)
    case 'DELAYS':
      return answerDelays(context)
    case 'RISKS':
      return answerRisks(context)
    case 'OVERALL_STATUS':
    default:
      return answerOverallStatus(context)
  }
}
