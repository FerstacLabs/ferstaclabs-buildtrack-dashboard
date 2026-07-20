import type {
  AttendanceSession,
  ConstructionObject,
  Crew,
  DailyForemanReport,
  EstimateVersion,
  MaterialItem,
  Project,
  ProjectIssue,
  ProjectProgressData,
  ProjectEstimateSummary,
  RiskEvent,
  WorkHourAllocation,
  WorkItem,
  WorkStage,
  WorkerAssignment,
} from '../../types/projectProgress'

export const villaProject: Project = {
  id: 'project-villa',
  name: 'Villa tikintisi',
  currency: 'AZN',
  location: 'Bakı, fərdi yaşayış sahəsi',
  clientName: 'FerstacLabs',
  createdAt: '2026-07-01T08:00:00.000Z',
  activeEstimateVersionId: 'estimate-current',
}

export const villaEstimateSummary: ProjectEstimateSummary = {
  totalAmount: 316_822.7,
  laborAmount: 69_717.5,
  materialAmount: 205_730.5,
  hiddenCostAmount: 41_324.7,
  currency: 'AZN',
}

export const estimateVersions: EstimateVersion[] = [
  {
    id: 'estimate-current',
    projectId: villaProject.id,
    name: 'Cari smeta',
    createdAt: '2026-07-01T08:00:00.000Z',
    totalAmount: villaEstimateSummary.totalAmount,
    notes: 'Villa tikintisi üçün təsdiqlənmiş ilkin smeta.',
  },
]

export const constructionObjects: ConstructionObject[] = [
  { id: 'obj-sea-breeze', name: 'Sea Breeze Residence', zone: 'Sahə', address: 'Bakı, Nardaran', projectId: villaProject.id, status: 'InProgress' },
  { id: 'obj-yasamal-towers', name: 'Yasamal City Towers', zone: '1-ci mərtəbə', address: 'Bakı, Yasamal', projectId: villaProject.id, status: 'InProgress' },
  { id: 'obj-nizami-residence', name: 'Nizami Park Rezidens', zone: 'Zirzəmi', address: 'Bakı, Nizami', projectId: villaProject.id, status: 'Delayed' },
  { id: 'obj-qobu-logistics', name: 'Qobu Logistika Mərkəzi', zone: 'Sahə', address: 'Qobu', projectId: villaProject.id, status: 'InProgress' },
  { id: 'obj-sumqayit-complex', name: 'Sumqayıt Yaşayış Kompleksi', zone: '2-ci mərtəbə', address: 'Sumqayıt', projectId: villaProject.id, status: 'Paused' },
  { id: 'obj-ganja-commerce', name: 'Gəncə Ticarət Mərkəzi', zone: 'Dam', address: 'Gəncə', projectId: villaProject.id, status: 'NotStarted' },
  { id: 'obj-lankaran-resort', name: 'Lənkəran İstirahət Kompleksi', zone: 'Həyət', address: 'Lənkəran', projectId: villaProject.id, status: 'InProgress' },
  { id: 'obj-baku-office', name: 'Bakı Ofis Mərkəzi', zone: '1-ci mərtəbə', address: 'Bakı, mərkəz', projectId: villaProject.id, status: 'Completed' },
]

const objectMultipliers: Record<string, { cost: number; progress: number; hours: number }> = {
  'obj-sea-breeze': { cost: 1.18, progress: 1, hours: 1.05 },
  'obj-yasamal-towers': { cost: 1.08, progress: 0.82, hours: 0.95 },
  'obj-nizami-residence': { cost: 0.92, progress: 0.65, hours: 0.88 },
  'obj-qobu-logistics': { cost: 0.74, progress: 0.72, hours: 0.78 },
  'obj-sumqayit-complex': { cost: 1.02, progress: 0.58, hours: 0.84 },
  'obj-ganja-commerce': { cost: 0.86, progress: 0.35, hours: 0.62 },
  'obj-lankaran-resort': { cost: 0.78, progress: 0.76, hours: 0.8 },
  'obj-baku-office': { cost: 0.69, progress: 1.08, hours: 0.72 },
}

const forObjectId = (objectId: string, id: string) => `${objectId}-${id}`

const scaleMoney = (value: number, objectId: string) => Math.round(value * objectMultipliers[objectId].cost * 100) / 100
const scaleHours = (value: number, objectId: string) => Math.round(value * objectMultipliers[objectId].hours)
const scaleProgress = (value: number, objectId: string) => Math.max(0, Math.min(100, Math.round(value * objectMultipliers[objectId].progress)))

const resolveStatusByProgress = (progress: number): WorkStage['status'] => {
  if (progress >= 100) return 'Completed'
  if (progress <= 0) return 'NotStarted'
  if (progress < 20) return 'Delayed'
  return 'InProgress'
}

export const projectCrews: Crew[] = [
  { id: 'crew-monolit', name: 'Monolit briqadası', type: 'Monolit', foremanName: 'Elvin Məmmədov', workerCount: 12, activeWorkStageId: 'stage-floor-1', activeWorkItemId: 'item-floor1-karkas', plannedDailyHours: 96, status: 'InProgress', progressPercent: 62, notes: 'Bünövrə və mərtəbə konstruksiyaları' },
  { id: 'crew-horgu', name: 'Hörgü briqadası', type: 'Hörgü', foremanName: 'Rəşad Əliyev', workerCount: 9, activeWorkStageId: 'stage-horgu', activeWorkItemId: 'item-horgu', plannedDailyHours: 72, status: 'Delayed', progressPercent: 18 },
  { id: 'crew-suvaq', name: 'Suvaq briqadası', type: 'Suvaq', foremanName: 'Namiq Quliyev', workerCount: 10, activeWorkStageId: 'stage-suvaq', activeWorkItemId: 'item-suvaq', plannedDailyHours: 80, status: 'Paused', progressPercent: 5 },
  { id: 'crew-dam', name: 'Dam briqadası', type: 'Dam', foremanName: 'Orxan Hüseynov', workerCount: 7, activeWorkStageId: 'stage-dam', activeWorkItemId: 'item-dam', plannedDailyHours: 56, status: 'NotStarted', progressPercent: 0 },
  { id: 'crew-pencere', name: 'Pəncərə/Qapı briqadası', type: 'Montaj', foremanName: 'Tural İsmayılov', workerCount: 6, activeWorkStageId: 'stage-qapi-pencere', activeWorkItemId: 'item-pencere', plannedDailyHours: 48, status: 'NotStarted', progressPercent: 0 },
  { id: 'crew-logistika', name: 'Material və logistika', type: 'Təchizat', foremanName: 'Səbuhi Kərimli', workerCount: 8, activeWorkStageId: 'stage-horgu', activeWorkItemId: 'item-horgu', plannedDailyHours: 64, status: 'InProgress', progressPercent: 35 },
]

export const projectStages: WorkStage[] = [
  { id: 'stage-torpaq', name: 'Torpaq işləri', order: 1, totalCost: 6850, laborCost: 2450, materialCost: 4400, plannedStartDate: '2026-07-01', plannedEndDate: '2026-07-06', status: 'Completed', progressPercent: 100, assignedCrewId: 'crew-logistika', plannedHours: 320, actualHours: 304, notes: 'Sahənin hazırlanması və qazıntı tamamlanıb.' },
  { id: 'stage-bunovre', name: 'Monolit dəmir beton lentvari bünövrə / Zirzəmi', order: 2, totalCost: 30311.4, laborCost: 7410, materialCost: 22901.4, plannedStartDate: '2026-07-07', plannedEndDate: '2026-07-24', status: 'Completed', progressPercent: 100, assignedCrewId: 'crew-monolit', plannedHours: 980, actualHours: 1016 },
  { id: 'stage-floor-1', name: 'Birinci mərtəbənin monolit d/beton konstruksiyaları', order: 3, totalCost: 67113.8, laborCost: 15125, materialCost: 51988.8, plannedStartDate: '2026-07-25', plannedEndDate: '2026-08-20', status: 'InProgress', progressPercent: 62, assignedCrewId: 'crew-monolit', plannedHours: 1560, actualHours: 1184 },
  { id: 'stage-floor-2', name: 'İkinci mərtəbənin monolit d/beton konstruksiyaları', order: 4, totalCost: 26632.8, laborCost: 6850, materialCost: 19782.8, plannedStartDate: '2026-08-21', plannedEndDate: '2026-09-08', status: 'NotStarted', progressPercent: 0, assignedCrewId: 'crew-monolit', plannedHours: 790, actualHours: 0 },
  { id: 'stage-dam', name: 'Dam örtüyü', order: 5, totalCost: 24750, laborCost: 6150, materialCost: 18600, plannedStartDate: '2026-09-09', plannedEndDate: '2026-09-22', status: 'NotStarted', progressPercent: 0, assignedCrewId: 'crew-dam', plannedHours: 420, actualHours: 0 },
  { id: 'stage-horgu', name: 'Hörgü işləri', order: 6, totalCost: 11970, laborCost: 4630, materialCost: 7340, plannedStartDate: '2026-08-12', plannedEndDate: '2026-09-16', status: 'Delayed', progressPercent: 18, assignedCrewId: 'crew-horgu', plannedHours: 680, actualHours: 176, notes: 'Kubik daşının çatdırılmasında gecikmə var.' },
  { id: 'stage-qapi-pencere', name: 'Qapı və pəncərələr', order: 7, totalCost: 20800, laborCost: 2500, materialCost: 18300, plannedStartDate: '2026-09-17', plannedEndDate: '2026-09-27', status: 'NotStarted', progressPercent: 0, assignedCrewId: 'crew-pencere', plannedHours: 180, actualHours: 0 },
  { id: 'stage-suvaq', name: 'Suvaq işləri', order: 8, totalCost: 87070, laborCost: 24602.5, materialCost: 62467.5, plannedStartDate: '2026-09-20', plannedEndDate: '2026-10-25', status: 'Paused', progressPercent: 5, assignedCrewId: 'crew-suvaq', plannedHours: 1420, actualHours: 64 },
  { id: 'stage-diger', name: 'Digər işlər', order: 9, totalCost: 0, laborCost: 0, materialCost: 0, plannedStartDate: '2026-10-26', plannedEndDate: '2026-10-30', status: 'NotStarted', progressPercent: 0, plannedHours: 0, actualHours: 0 },
]

export const projectWorkItems: WorkItem[] = [
  { id: 'item-qazinti', stageId: 'stage-torpaq', name: 'Torpaq qazıntısı və sahənin hazırlanması', costCode: 'TOR-001', unit: 'iş', quantity: 1, completedQuantity: 1, unitPrice: 6850, laborUnitPrice: 2450, laborTotal: 2450, materialUnit: 'iş', materialQuantity: 1, materialUnitPrice: 4400, materialTotal: 4400, totalCost: 6850, plannedHours: 320, actualHours: 304, remainingHours: 0, assignedCrewId: 'crew-logistika', status: 'Completed', progressPercent: 100, plannedStartDate: '2026-07-01', plannedEndDate: '2026-07-06' },
  { id: 'item-beton-bunovre', stageId: 'stage-bunovre', name: 'Bünövrə beton və armatur işləri', costCode: 'MON-001', unit: 'm3', quantity: 58, completedQuantity: 58, unitPrice: 522.61, laborUnitPrice: 127.76, laborTotal: 7410, materialUnit: 'm3', materialQuantity: 58, materialUnitPrice: 394.85, materialTotal: 22901.4, totalCost: 30311.4, plannedHours: 980, actualHours: 1016, remainingHours: 0, assignedCrewId: 'crew-monolit', status: 'Completed', progressPercent: 100, plannedStartDate: '2026-07-07', plannedEndDate: '2026-07-24' },
  { id: 'item-floor1-karkas', stageId: 'stage-floor-1', name: '1-ci mərtəbə qəlib, armatur və beton', costCode: 'MON-101', unit: 'm2', quantity: 420, completedQuantity: 260, unitPrice: 159.79, laborUnitPrice: 36.01, laborTotal: 15125, materialUnit: 'm2', materialQuantity: 420, materialUnitPrice: 123.78, materialTotal: 51988.8, totalCost: 67113.8, plannedHours: 1560, actualHours: 1184, remainingHours: 376, assignedCrewId: 'crew-monolit', status: 'InProgress', progressPercent: 62, plannedStartDate: '2026-07-25', plannedEndDate: '2026-08-20' },
  { id: 'item-floor2-karkas', stageId: 'stage-floor-2', name: '2-ci mərtəbə monolit konstruksiya', costCode: 'MON-201', unit: 'm2', quantity: 260, completedQuantity: 0, unitPrice: 102.43, laborUnitPrice: 26.35, laborTotal: 6850, materialUnit: 'm2', materialQuantity: 260, materialUnitPrice: 76.09, materialTotal: 19782.8, totalCost: 26632.8, plannedHours: 790, actualHours: 0, remainingHours: 790, assignedCrewId: 'crew-monolit', status: 'NotStarted', progressPercent: 0, plannedStartDate: '2026-08-21', plannedEndDate: '2026-09-08' },
  { id: 'item-dam', stageId: 'stage-dam', name: 'Dam örtüyü və taxta konstruksiya', costCode: 'DAM-001', unit: 'm2', quantity: 330, completedQuantity: 0, unitPrice: 75, laborUnitPrice: 18.64, laborTotal: 6150, materialUnit: 'm2', materialQuantity: 330, materialUnitPrice: 56.36, materialTotal: 18600, totalCost: 24750, plannedHours: 420, actualHours: 0, remainingHours: 420, assignedCrewId: 'crew-dam', status: 'NotStarted', progressPercent: 0, plannedStartDate: '2026-09-09', plannedEndDate: '2026-09-22' },
  { id: 'item-horgu', stageId: 'stage-horgu', name: 'Kubik daş hörgüsü', costCode: 'HOR-001', unit: 'm2', quantity: 1270, completedQuantity: 230, unitPrice: 9.43, laborUnitPrice: 3.65, laborTotal: 4630, materialUnit: 'm2', materialQuantity: 1270, materialUnitPrice: 5.78, materialTotal: 7340, totalCost: 11970, plannedHours: 680, actualHours: 176, remainingHours: 504, assignedCrewId: 'crew-horgu', status: 'Delayed', progressPercent: 18, plannedStartDate: '2026-08-12', plannedEndDate: '2026-09-16', notes: 'Material çatdırılma tarixi yenilənməlidir.' },
  { id: 'item-pencere', stageId: 'stage-qapi-pencere', name: 'Alüminyum pəncərə və qapı montajı', costCode: 'QAP-001', unit: 'm2', quantity: 65, completedQuantity: 0, unitPrice: 320, laborUnitPrice: 38.46, laborTotal: 2500, materialUnit: 'm2', materialQuantity: 65, materialUnitPrice: 281.54, materialTotal: 18300, totalCost: 20800, plannedHours: 180, actualHours: 0, remainingHours: 180, assignedCrewId: 'crew-pencere', status: 'NotStarted', progressPercent: 0, plannedStartDate: '2026-09-17', plannedEndDate: '2026-09-27' },
  { id: 'item-suvaq', stageId: 'stage-suvaq', name: 'Daxili və xarici suvaq işləri', costCode: 'SUV-001', unit: 'm2', quantity: 1900, completedQuantity: 95, unitPrice: 45.83, laborUnitPrice: 12.95, laborTotal: 24602.5, materialUnit: 'm2', materialQuantity: 1900, materialUnitPrice: 32.88, materialTotal: 62467.5, totalCost: 87070, plannedHours: 1420, actualHours: 64, remainingHours: 1356, assignedCrewId: 'crew-suvaq', status: 'Paused', progressPercent: 5, plannedStartDate: '2026-09-20', plannedEndDate: '2026-10-25' },
]

export const projectMaterials: MaterialItem[] = [
  { id: 'mat-armatur-a3', name: 'Armatur A3', unit: 'ton', quantity: 20.75, usedQuantity: 13.9, remainingQuantity: 6.85, unitPrice: 1450, linkedStageId: 'stage-floor-1', linkedWorkItemId: 'item-floor1-karkas', deliveryDate: '2026-07-28', supplier: 'Bakı Metal' },
  { id: 'mat-armatur-a1', name: 'Armatur A1', unit: 'ton', quantity: 4.2, usedQuantity: 2.6, remainingQuantity: 1.6, unitPrice: 1320, linkedStageId: 'stage-bunovre', supplier: 'Bakı Metal' },
  { id: 'mat-taxta', name: 'Taxta', unit: 'm3', quantity: 19, usedQuantity: 11, remainingQuantity: 8, unitPrice: 310, linkedStageId: 'stage-dam', supplier: 'Woodline' },
  { id: 'mat-dikt', name: 'Dikt', unit: 'ədəd', quantity: 280, usedQuantity: 160, remainingQuantity: 120, unitPrice: 18, linkedStageId: 'stage-floor-1' },
  { id: 'mat-beton-b75', name: 'Beton B7.5', unit: 'm3', quantity: 18.3, usedQuantity: 18.3, remainingQuantity: 0, unitPrice: 92, linkedStageId: 'stage-bunovre' },
  { id: 'mat-beton-b25', name: 'Beton B25', unit: 'm3', quantity: 328.2, usedQuantity: 182, remainingQuantity: 146.2, unitPrice: 118, linkedStageId: 'stage-floor-1', linkedWorkItemId: 'item-floor1-karkas' },
  { id: 'mat-cinqil', name: 'Çınqıl', unit: 'm3', quantity: 16.1, usedQuantity: 16.1, remainingQuantity: 0, unitPrice: 42, linkedStageId: 'stage-torpaq' },
  { id: 'mat-pencere', name: 'Alüminyum pəncərə', unit: 'm2', quantity: 65, usedQuantity: 0, remainingQuantity: 65, unitPrice: 281.54, linkedStageId: 'stage-qapi-pencere', supplier: 'AluTech' },
  { id: 'mat-aqlay', name: 'Aqlay daşı', unit: 'm2', quantity: 515, usedQuantity: 0, remainingQuantity: 515, unitPrice: 28, linkedStageId: 'stage-suvaq' },
  { id: 'mat-kubik', name: 'Kubik daşı', unit: 'm2', quantity: 1270, usedQuantity: 230, remainingQuantity: 1040, unitPrice: 5.78, linkedStageId: 'stage-horgu', linkedWorkItemId: 'item-horgu', deliveryDate: '2026-08-18', supplier: 'Daş Market', notes: 'Növbəti partiya gecikir.' },
]

const workerNames = [
  'İlham Əliyev', 'Rauf Hüseynli', 'Vüqar Məmmədov', 'Elçin Quliyev', 'Nicat Qurbanov', 'Orxan Rzayev', 'Murad Abbasov', 'Fərid Həsənov', 'Anar Cəfərov', 'Kamran İsmayılov', 'Emin Səfərov', 'Tural Nəcəfov',
  'Tahirə Məmmədova', 'Ramil Əhmədov', 'Sadiq Əliyev', 'Cavid Qasımov', 'Şahin Mustafayev', 'Əli Babayev', 'Pərviz Məmmədli', 'Nurlan Kərimov', 'Elnur Ağayev',
  'Samir Qasımov', 'Namiq Sadıqov', 'Rəşad Məlikov', 'Fuad Osmanov', 'Məhəmməd Vəliyev', 'Sənan Hacıyev', 'Ruslan Əlizadə', 'Rövşən Məmmədov', 'Mətin Qurbanlı', 'Kənan Hüseynov',
  'Azər Bayramov', 'Ceyhun Rüstəmov', 'Qadir Məmmədov', 'Seymur Əliyev', 'Rəşid Orucov', 'Tofiq Quliyev', 'Mikayıl Əkbərov',
  'Tural İsmayılov', 'Əkbər Məmmədli', 'Elvin Rəhimov', 'Səxavət Qurbanov', 'Cəlal Hüseynli', 'Nurlana Əliyeva',
  'Səbuhi Kərimli', 'Bəxtiyar Abbasov', 'Zaur Həsənov', 'Taleh Məmmədov', 'Aydın Hüseynov', 'Vüsal Əhmədov', 'Ramin İbrahimov', 'Qurban Vəliyev',
]

const crewWorkerSeeds = [
  { crewId: 'crew-monolit', count: 12, stageId: 'stage-floor-1', workItemId: 'item-floor1-karkas', roles: ['Betonçu', 'Armaturçu', 'Qəlib ustası'], hourlyRate: 8.1 },
  { crewId: 'crew-horgu', count: 9, stageId: 'stage-horgu', workItemId: 'item-horgu', roles: ['Hörgü ustası', 'Köməkçi işçi'], hourlyRate: 7.4 },
  { crewId: 'crew-suvaq', count: 10, stageId: 'stage-suvaq', workItemId: 'item-suvaq', roles: ['Suvaqçı', 'Köməkçi işçi'], hourlyRate: 7.7 },
  { crewId: 'crew-dam', count: 7, stageId: 'stage-dam', workItemId: 'item-dam', roles: ['Dam ustası', 'Taxta ustası'], hourlyRate: 8.4 },
  { crewId: 'crew-pencere', count: 6, stageId: 'stage-qapi-pencere', workItemId: 'item-pencere', roles: ['Montaj ustası', 'Pəncərə ustası'], hourlyRate: 8.8 },
  { crewId: 'crew-logistika', count: 8, stageId: 'stage-horgu', workItemId: 'item-horgu', roles: ['Logistika', 'Anbardar', 'Sürücü'], hourlyRate: 6.8 },
]

const makeWorkerId = (index: number) => `worker-${String(index + 1).padStart(4, '0')}`
const makeExternalId = (index: number) => `W-${String(index + 1).padStart(4, '0')}`

export const projectWorkerAssignments: WorkerAssignment[] = crewWorkerSeeds.flatMap((seed, seedIndex) => {
  const offset = crewWorkerSeeds.slice(0, seedIndex).reduce((sum, item) => sum + item.count, 0)
  return Array.from({ length: seed.count }, (_, localIndex) => {
    const index = offset + localIndex
    return {
      id: makeWorkerId(index),
      workerName: workerNames[index],
      workerExternalId: makeExternalId(index),
      projectId: villaProject.id,
      crewId: seed.crewId,
      role: seed.roles[localIndex % seed.roles.length],
      hourlyRate: Math.round((seed.hourlyRate + (localIndex % 4) * 0.35) * 100) / 100,
      plannedDailyHours: 8,
      activeStageId: seed.stageId,
      activeWorkItemId: seed.workItemId,
      attendanceSource: index % 5 === 0 ? 'Manual' : index % 4 === 0 ? 'ForemanTablet' : 'Camera',
      status: index === 31 || index === 47 ? 'inactive' : 'active',
      riskScore: index % 13 === 0 ? 68 : index % 9 === 0 ? 42 : 8 + ((index * 7) % 24),
      notes: index % 13 === 0 ? 'Davamiyyət və performans izlənməlidir.' : undefined,
    }
  })
})

const workerMonthlyHours = (index: number) => 158 + ((index * 13) % 58)

const monthlyAttendanceSessions: AttendanceSession[] = projectWorkerAssignments.map((worker, index) => ({
  id: `att-may-${worker.id}`,
  workerId: worker.id,
  workerExternalId: worker.workerExternalId,
  projectId: villaProject.id,
  date: '2025-05-31',
  firstSeen: '2025-05-01T04:00:00.000Z',
  lastSeen: '2025-05-31T14:00:00.000Z',
  totalHours: workerMonthlyHours(index),
  source: worker.attendanceSource === 'Camera' ? 'Dahua' : worker.attendanceSource,
  deviceId: worker.attendanceSource === 'Camera' ? 'dahua-entry-1' : undefined,
}))

const todayAttendanceSessions: AttendanceSession[] = projectWorkerAssignments.slice(0, 12).map((worker, index) => ({
  id: `att-today-${worker.id}`,
  workerId: worker.id,
  workerExternalId: worker.workerExternalId,
  projectId: villaProject.id,
  date: '2026-07-20',
  firstSeen: '2026-07-20T04:00:00.000Z',
  lastSeen: '2026-07-20T12:00:00.000Z',
  totalHours: Math.round((7.2 + (index % 5) * 0.35) * 10) / 10,
  source: worker.attendanceSource === 'Camera' ? 'Dahua' : worker.attendanceSource,
  deviceId: worker.attendanceSource === 'Camera' ? 'dahua-entry-1' : undefined,
}))

export const attendanceSessions: AttendanceSession[] = [...monthlyAttendanceSessions, ...todayAttendanceSessions]

const monthlyWorkHourAllocations: WorkHourAllocation[] = projectWorkerAssignments.map((worker, index) => ({
  id: `alloc-may-${worker.id}`,
  attendanceSessionId: `att-may-${worker.id}`,
  workerId: worker.id,
  projectId: villaProject.id,
  crewId: worker.crewId,
  stageId: worker.activeStageId ?? 'stage-floor-1',
  workItemId: worker.activeWorkItemId ?? 'item-floor1-karkas',
  date: '2025-05-31',
  hours: workerMonthlyHours(index),
  allocationPercent: 100,
  source: worker.attendanceSource === 'Camera' ? 'auto' : worker.attendanceSource === 'Manual' ? 'manual' : 'prorab',
}))

const todayWorkHourAllocations: WorkHourAllocation[] = projectWorkerAssignments.slice(0, 12).map((worker, index) => ({
  id: `alloc-today-${worker.id}`,
  attendanceSessionId: `att-today-${worker.id}`,
  workerId: worker.id,
  projectId: villaProject.id,
  crewId: worker.crewId,
  stageId: worker.activeStageId ?? 'stage-floor-1',
  workItemId: worker.activeWorkItemId ?? 'item-floor1-karkas',
  date: '2026-07-20',
  hours: Math.round((7.2 + (index % 5) * 0.35) * 10) / 10,
  allocationPercent: 100,
  source: worker.attendanceSource === 'Camera' ? 'auto' : worker.attendanceSource === 'Manual' ? 'manual' : 'prorab',
}))

export const workHourAllocations: WorkHourAllocation[] = [...monthlyWorkHourAllocations, ...todayWorkHourAllocations]

export const dailyReports: DailyForemanReport[] = [
  {
    id: 'report-1',
    projectId: villaProject.id,
    date: '2026-07-20',
    weather: 'Günəşli',
    foremanName: 'Elvin Məmmədov',
    crewIds: ['crew-monolit'],
    workedItemIds: ['item-floor1-karkas'],
    completedWorks: [{ workItemId: 'item-floor1-karkas', completedQuantity: 18, notes: 'Qəlib bağlama və armatur tamamlandı.' }],
    todayNotes: '1-ci mərtəbə monolit karkas üzrə qəlib və armatur işləri davam etdirildi.',
    remainingNotes: 'Sabah beton tökülüşünə hazırlıq tamamlanmalıdır.',
    status: 'Submitted',
    photoCount: 4,
    photos: [],
    createdAt: '2026-07-20T14:00:00.000Z',
  },
  {
    id: 'report-2',
    projectId: villaProject.id,
    date: '2026-07-19',
    weather: 'Küləkli',
    foremanName: 'Rəşad Əliyev',
    crewIds: ['crew-horgu'],
    workedItemIds: ['item-horgu'],
    completedWorks: [{ workItemId: 'item-horgu', completedQuantity: 22 }],
    todayNotes: 'Hörgü xətti davam etdi, material çatdırılması günün ikinci yarısında gecikdi.',
    delayReason: 'Kubik daşının növbəti partiyası gec çatdırıldı.',
    materialShortage: 'Kubik daşı ehtiyatı kritik səviyyəyə yaxınlaşır.',
    status: 'Approved',
    photoCount: 2,
    photos: [],
    createdAt: '2026-07-19T14:20:00.000Z',
  },
]

export const projectIssues: ProjectIssue[] = [
  { id: 'issue-1', projectId: villaProject.id, stageId: 'stage-horgu', workItemId: 'item-horgu', type: 'Material', title: 'Kubik daşı çatdırılması gecikir', severity: 'High', status: 'Open', dueDate: '2026-07-22', createdAt: '2026-07-19T14:30:00.000Z' },
  { id: 'issue-2', projectId: villaProject.id, stageId: 'stage-suvaq', workItemId: 'item-suvaq', type: 'Schedule', title: 'Suvaq briqadası sahəyə tam başlamayıb', severity: 'Medium', status: 'Watching', dueDate: '2026-09-20', createdAt: '2026-07-18T08:00:00.000Z' },
]

export const projectRisks: RiskEvent[] = [
  { id: 'risk-1', projectId: villaProject.id, stageId: 'stage-horgu', crewId: 'crew-horgu', title: 'Hörgü işi plan qrafikindən geri qalır', severity: 'High', source: 'schedule', createdAt: '2026-07-20T09:00:00.000Z' },
  { id: 'risk-2', projectId: villaProject.id, workerId: 'worker-3', crewId: 'crew-suvaq', title: 'Manual saat düzəlişləri izlənməlidir', severity: 'Medium', source: 'attendance', createdAt: '2026-07-20T10:00:00.000Z' },
]

const objectStages = constructionObjects.flatMap((object) =>
  projectStages.map((stage) => {
    const progressPercent = scaleProgress(stage.progressPercent, object.id)
    return {
      ...stage,
      id: forObjectId(object.id, stage.id),
      objectId: object.id,
      totalCost: scaleMoney(stage.totalCost, object.id),
      laborCost: scaleMoney(stage.laborCost, object.id),
      materialCost: scaleMoney(stage.materialCost, object.id),
      assignedCrewId: stage.assignedCrewId ? forObjectId(object.id, stage.assignedCrewId) : undefined,
      plannedHours: scaleHours(stage.plannedHours, object.id),
      actualHours: scaleHours(stage.actualHours, object.id),
      progressPercent,
      status: object.status === 'Completed' ? 'Completed' : resolveStatusByProgress(progressPercent),
    } satisfies WorkStage
  }),
)

const objectWorkItems = constructionObjects.flatMap((object) =>
  projectWorkItems.map((item) => {
    const progressPercent = scaleProgress(item.progressPercent, object.id)
    return {
      ...item,
      id: forObjectId(object.id, item.id),
      objectId: object.id,
      stageId: forObjectId(object.id, item.stageId),
      assignedCrewId: item.assignedCrewId ? forObjectId(object.id, item.assignedCrewId) : undefined,
      laborTotal: scaleMoney(item.laborTotal, object.id),
      materialTotal: scaleMoney(item.materialTotal, object.id),
      totalCost: scaleMoney(item.totalCost, object.id),
      laborUnitPrice: scaleMoney(item.laborUnitPrice, object.id),
      materialUnitPrice: scaleMoney(item.materialUnitPrice, object.id),
      plannedHours: scaleHours(item.plannedHours, object.id),
      actualHours: scaleHours(item.actualHours, object.id),
      remainingHours: Math.max(0, scaleHours(item.plannedHours - item.actualHours, object.id)),
      completedQuantity: item.quantity ? Math.round(item.quantity * progressPercent) / 100 : item.completedQuantity,
      progressPercent,
      status: object.status === 'Completed' ? 'Completed' : resolveStatusByProgress(progressPercent),
    } satisfies WorkItem
  }),
)

const objectCrews = constructionObjects.flatMap((object) =>
  projectCrews.map((crew) => {
    const progressPercent = scaleProgress(crew.progressPercent ?? 0, object.id)
    return {
      ...crew,
      id: forObjectId(object.id, crew.id),
      objectId: object.id,
      activeWorkStageId: crew.activeWorkStageId ? forObjectId(object.id, crew.activeWorkStageId) : undefined,
      activeWorkItemId: crew.activeWorkItemId ? forObjectId(object.id, crew.activeWorkItemId) : undefined,
      workerIds: [],
      plannedDailyHours: scaleHours(crew.plannedDailyHours, object.id),
      progressPercent,
      status: object.status === 'Completed' ? 'Completed' : resolveStatusByProgress(progressPercent),
    } satisfies Crew
  }),
)

const objectWorkerAssignments = constructionObjects.flatMap((object, objectIndex) =>
  projectWorkerAssignments.map((worker, workerIndex) => ({
    ...worker,
    id: forObjectId(object.id, worker.id),
    workerExternalId: `W-${String(objectIndex + 1).padStart(2, '0')}-${String(workerIndex + 1).padStart(4, '0')}`,
    projectId: villaProject.id,
    objectId: object.id,
    crewId: forObjectId(object.id, worker.crewId),
    activeStageId: worker.activeStageId ? forObjectId(object.id, worker.activeStageId) : undefined,
    activeWorkItemId: worker.activeWorkItemId ? forObjectId(object.id, worker.activeWorkItemId) : undefined,
    hourlyRate: Math.round(worker.hourlyRate * objectMultipliers[object.id].cost * 100) / 100,
    riskScore: Math.min(100, Math.round(worker.riskScore * (object.status === 'Delayed' ? 1.28 : object.status === 'Paused' ? 1.15 : 0.92))),
  } satisfies WorkerAssignment)),
)

const objectAttendanceSessions = constructionObjects.flatMap((object) =>
  attendanceSessions.map((session) => ({
    ...session,
    id: forObjectId(object.id, session.id),
    workerId: session.workerId ? forObjectId(object.id, session.workerId) : undefined,
    objectId: object.id,
    totalHours: Math.round(session.totalHours * objectMultipliers[object.id].hours * 10) / 10,
  } satisfies AttendanceSession)),
)

const objectWorkHourAllocations = constructionObjects.flatMap((object) =>
  workHourAllocations.map((allocation) => ({
    ...allocation,
    id: forObjectId(object.id, allocation.id),
    attendanceSessionId: forObjectId(object.id, allocation.attendanceSessionId),
    workerId: forObjectId(object.id, allocation.workerId),
    objectId: object.id,
    crewId: forObjectId(object.id, allocation.crewId),
    stageId: forObjectId(object.id, allocation.stageId),
    workItemId: forObjectId(object.id, allocation.workItemId),
    hours: Math.round(allocation.hours * objectMultipliers[object.id].hours * 10) / 10,
  } satisfies WorkHourAllocation)),
)

const objectMaterials = constructionObjects.flatMap((object) =>
  projectMaterials.map((material) => {
    const quantity = Math.round(material.quantity * objectMultipliers[object.id].cost * 10) / 10
    const usedQuantity = Math.round(material.usedQuantity * objectMultipliers[object.id].progress * 10) / 10
    return {
      ...material,
      id: forObjectId(object.id, material.id),
      objectId: object.id,
      quantity,
      usedQuantity,
      remainingQuantity: Math.max(0, Math.round((quantity - usedQuantity) * 10) / 10),
      unitPrice: material.unitPrice ? scaleMoney(material.unitPrice, object.id) : undefined,
      linkedStageId: material.linkedStageId ? forObjectId(object.id, material.linkedStageId) : undefined,
      linkedWorkItemId: material.linkedWorkItemId ? forObjectId(object.id, material.linkedWorkItemId) : undefined,
    } satisfies MaterialItem
  }),
)

const objectDailyReports = constructionObjects.flatMap((object) =>
  dailyReports.map((report) => ({
    ...report,
    id: forObjectId(object.id, report.id),
    objectId: object.id,
    crewIds: report.crewIds.map((crewId) => forObjectId(object.id, crewId)),
    workedItemIds: report.workedItemIds.map((itemId) => forObjectId(object.id, itemId)),
    completedWorks: report.completedWorks.map((work) => ({ ...work, workItemId: forObjectId(object.id, work.workItemId) })),
    todayNotes: `${object.name}: ${report.todayNotes}`,
  } satisfies DailyForemanReport)),
)

const objectIssues = constructionObjects.flatMap((object) =>
  projectIssues.map((issue) => ({
    ...issue,
    id: forObjectId(object.id, issue.id),
    objectId: object.id,
    stageId: issue.stageId ? forObjectId(object.id, issue.stageId) : undefined,
    workItemId: issue.workItemId ? forObjectId(object.id, issue.workItemId) : undefined,
    title: `${object.name}: ${issue.title}`,
  } satisfies ProjectIssue)),
)

const objectRisks = constructionObjects.flatMap((object) =>
  projectRisks.map((risk) => ({
    ...risk,
    id: forObjectId(object.id, risk.id),
    objectId: object.id,
    stageId: risk.stageId ? forObjectId(object.id, risk.stageId) : undefined,
    crewId: risk.crewId ? forObjectId(object.id, risk.crewId) : undefined,
    workerId: risk.workerId ? forObjectId(object.id, risk.workerId) : undefined,
    title: `${object.name}: ${risk.title}`,
  } satisfies RiskEvent)),
)

const objectSummary: ProjectEstimateSummary = {
  totalAmount: Math.round(objectStages.reduce((sum, stage) => sum + stage.totalCost, 0) * 100) / 100,
  laborAmount: Math.round(objectStages.reduce((sum, stage) => sum + stage.laborCost, 0) * 100) / 100,
  materialAmount: Math.round(objectStages.reduce((sum, stage) => sum + stage.materialCost, 0) * 100) / 100,
  hiddenCostAmount: Math.round(villaEstimateSummary.hiddenCostAmount * constructionObjects.length * 100) / 100,
  currency: 'AZN',
}

export const projectProgressSeed: ProjectProgressData = {
  projects: [villaProject],
  activeProjectId: villaProject.id,
  objects: constructionObjects,
  selectedObjectIdByPage: {},
  project: villaProject,
  estimateVersions,
  summary: objectSummary,
  stages: objectStages,
  workItems: objectWorkItems,
  crews: objectCrews,
  workerAssignments: objectWorkerAssignments,
  materials: objectMaterials,
  attendanceSessions: objectAttendanceSessions,
  workHourAllocations: objectWorkHourAllocations,
  dailyReports: objectDailyReports,
  issues: objectIssues,
  risks: objectRisks,
  assistantMessages: [],
}
