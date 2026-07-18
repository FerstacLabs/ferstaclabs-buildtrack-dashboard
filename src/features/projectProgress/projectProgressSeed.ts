import type { Crew, MaterialItem, ProjectEstimateSummary, ProjectProgressData, WorkItem, WorkStage, WorkerAssignment } from '../../types/projectProgress'

export const villaEstimateSummary: ProjectEstimateSummary = {
  totalAmount: 316_822.7,
  laborAmount: 69_717.5,
  materialAmount: 205_730.5,
  hiddenCostAmount: 41_324.7,
  currency: 'AZN',
}

export const projectCrews: Crew[] = [
  { id: 'crew-monolit', name: 'Monolit briqadası', type: 'Dəmir-beton', foremanName: 'Elvin Məmmədov', workerCount: 14, activeWorkStageId: 'stage-floor-1', plannedDailyHours: 112, notes: 'Bünövrə və mərtəbə konstruksiyaları' },
  { id: 'crew-horgu', name: 'Hörgü briqadası', type: 'Hörgü', foremanName: 'Rəşad Əliyev', workerCount: 10, activeWorkStageId: 'stage-horgu', plannedDailyHours: 80 },
  { id: 'crew-suvaq', name: 'Suvaq briqadası', type: 'Suvaq', foremanName: 'Namiq Quliyev', workerCount: 12, activeWorkStageId: 'stage-suvaq', plannedDailyHours: 96 },
  { id: 'crew-dam', name: 'Dam briqadası', type: 'Dam örtüyü', foremanName: 'Orxan Hüseynov', workerCount: 7, activeWorkStageId: 'stage-dam', plannedDailyHours: 56 },
  { id: 'crew-pencere', name: 'Pəncərə/Qapı briqadası', type: 'Montaj', foremanName: 'Tural İsmayılov', workerCount: 5, activeWorkStageId: 'stage-qapi-pencere', plannedDailyHours: 40 },
  { id: 'crew-logistika', name: 'Material və logistika', type: 'Təchizat', foremanName: 'Səbuhi Kərimli', workerCount: 4, plannedDailyHours: 32 },
]

export const projectStages: WorkStage[] = [
  { id: 'stage-torpaq', name: 'Torpaq işləri', order: 1, totalCost: 6850, laborCost: 2450, materialCost: 4400, plannedStartDate: '2026-07-01', plannedEndDate: '2026-07-06', status: 'Completed', progressPercent: 100, assignedCrewId: 'crew-logistika', plannedHours: 320, actualHours: 304, notes: 'Sahənin hazırlanması və qazıntı' },
  { id: 'stage-bunovre', name: 'Monolit dəmir beton lentvari bünövrə / Zirzəmi', order: 2, totalCost: 30311.4, laborCost: 7410, materialCost: 22901.4, plannedStartDate: '2026-07-07', plannedEndDate: '2026-07-24', status: 'Completed', progressPercent: 100, assignedCrewId: 'crew-monolit', plannedHours: 980, actualHours: 1016 },
  { id: 'stage-floor-1', name: 'Birinci mərtəbənin monolit d/beton konstruksiyaları', order: 3, totalCost: 67113.8, laborCost: 15125, materialCost: 51988.8, plannedStartDate: '2026-07-25', plannedEndDate: '2026-08-20', status: 'InProgress', progressPercent: 62, assignedCrewId: 'crew-monolit', plannedHours: 1560, actualHours: 1184 },
  { id: 'stage-floor-2', name: 'İkinci mərtəbənin monolit d/beton konstruksiyaları', order: 4, totalCost: 26632.8, laborCost: 6850, materialCost: 19782.8, plannedStartDate: '2026-08-21', plannedEndDate: '2026-09-08', status: 'NotStarted', progressPercent: 0, assignedCrewId: 'crew-monolit', plannedHours: 790, actualHours: 0 },
  { id: 'stage-dam', name: 'Dam örtüyü', order: 5, totalCost: 24750, laborCost: 6150, materialCost: 18600, plannedStartDate: '2026-09-09', plannedEndDate: '2026-09-22', status: 'NotStarted', progressPercent: 0, assignedCrewId: 'crew-dam', plannedHours: 420, actualHours: 0 },
  { id: 'stage-horgu', name: 'Hörgü işləri', order: 6, totalCost: 11970, laborCost: 4630, materialCost: 7340, plannedStartDate: '2026-08-12', plannedEndDate: '2026-09-16', status: 'Delayed', progressPercent: 18, assignedCrewId: 'crew-horgu', plannedHours: 680, actualHours: 176, notes: 'Kubik daşının çatdırılmasında gecikmə var' },
  { id: 'stage-qapi-pencere', name: 'Qapı və pəncərələr', order: 7, totalCost: 20800, laborCost: 2500, materialCost: 18300, plannedStartDate: '2026-09-17', plannedEndDate: '2026-09-27', status: 'NotStarted', progressPercent: 0, assignedCrewId: 'crew-pencere', plannedHours: 180, actualHours: 0 },
  { id: 'stage-suvaq', name: 'Suvaq işləri', order: 8, totalCost: 87070, laborCost: 24602.5, materialCost: 62467.5, plannedStartDate: '2026-09-20', plannedEndDate: '2026-10-25', status: 'Paused', progressPercent: 5, assignedCrewId: 'crew-suvaq', plannedHours: 1420, actualHours: 64 },
  { id: 'stage-diger', name: 'Digər işlər', order: 9, totalCost: 0, laborCost: 0, materialCost: 0, plannedStartDate: '2026-10-26', plannedEndDate: '2026-10-30', status: 'NotStarted', progressPercent: 0, plannedHours: 0, actualHours: 0 },
]

export const projectWorkItems: WorkItem[] = [
  { id: 'item-qazinti', stageId: 'stage-torpaq', name: 'Torpaq qazıntısı və sahənin hazırlanması', unit: 'iş', quantity: 1, laborUnitPrice: 2450, laborTotal: 2450, materialUnit: 'iş', materialQuantity: 1, materialUnitPrice: 4400, materialTotal: 4400, totalCost: 6850, plannedHours: 320, actualHours: 304, assignedCrewId: 'crew-logistika', status: 'Completed', progressPercent: 100 },
  { id: 'item-beton-bunovre', stageId: 'stage-bunovre', name: 'Bünövrə beton və armatur işləri', unit: 'm3', quantity: 58, laborUnitPrice: 127.76, laborTotal: 7410, materialUnit: 'm3', materialQuantity: 58, materialUnitPrice: 394.85, materialTotal: 22901.4, totalCost: 30311.4, plannedHours: 980, actualHours: 1016, assignedCrewId: 'crew-monolit', status: 'Completed', progressPercent: 100 },
  { id: 'item-floor1-karkas', stageId: 'stage-floor-1', name: '1-ci mərtəbə qəlib, armatur və beton', unit: 'm2', quantity: 420, laborUnitPrice: 36.01, laborTotal: 15125, materialUnit: 'm2', materialQuantity: 420, materialUnitPrice: 123.78, materialTotal: 51988.8, totalCost: 67113.8, plannedHours: 1560, actualHours: 1184, assignedCrewId: 'crew-monolit', status: 'InProgress', progressPercent: 62 },
  { id: 'item-floor2-karkas', stageId: 'stage-floor-2', name: '2-ci mərtəbə monolit konstruksiya', unit: 'm2', quantity: 260, laborUnitPrice: 26.35, laborTotal: 6850, materialUnit: 'm2', materialQuantity: 260, materialUnitPrice: 76.09, materialTotal: 19782.8, totalCost: 26632.8, plannedHours: 790, actualHours: 0, assignedCrewId: 'crew-monolit', status: 'NotStarted', progressPercent: 0 },
  { id: 'item-dam', stageId: 'stage-dam', name: 'Dam örtüyü və taxta konstruksiya', unit: 'm2', quantity: 330, laborUnitPrice: 18.64, laborTotal: 6150, materialUnit: 'm2', materialQuantity: 330, materialUnitPrice: 56.36, materialTotal: 18600, totalCost: 24750, plannedHours: 420, actualHours: 0, assignedCrewId: 'crew-dam', status: 'NotStarted', progressPercent: 0 },
  { id: 'item-horgu', stageId: 'stage-horgu', name: 'Kubik daş hörgüsü', unit: 'm2', quantity: 1270, laborUnitPrice: 3.65, laborTotal: 4630, materialUnit: 'm2', materialQuantity: 1270, materialUnitPrice: 5.78, materialTotal: 7340, totalCost: 11970, plannedHours: 680, actualHours: 176, assignedCrewId: 'crew-horgu', status: 'Delayed', progressPercent: 18 },
  { id: 'item-pencere', stageId: 'stage-qapi-pencere', name: 'Alüminyum pəncərə və qapı montajı', unit: 'm2', quantity: 65, laborUnitPrice: 38.46, laborTotal: 2500, materialUnit: 'm2', materialQuantity: 65, materialUnitPrice: 281.54, materialTotal: 18300, totalCost: 20800, plannedHours: 180, actualHours: 0, assignedCrewId: 'crew-pencere', status: 'NotStarted', progressPercent: 0 },
  { id: 'item-suvaq', stageId: 'stage-suvaq', name: 'Daxili və xarici suvaq işləri', unit: 'm2', quantity: 1900, laborUnitPrice: 12.95, laborTotal: 24602.5, materialUnit: 'm2', materialQuantity: 1900, materialUnitPrice: 32.88, materialTotal: 62467.5, totalCost: 87070, plannedHours: 1420, actualHours: 64, assignedCrewId: 'crew-suvaq', status: 'Paused', progressPercent: 5 },
]

export const projectMaterials: MaterialItem[] = [
  { id: 'mat-armatur-a3', name: 'Armatur A3', unit: 'ton', quantity: 20.75, usedQuantity: 13.9, remainingQuantity: 6.85, linkedStageId: 'stage-floor-1' },
  { id: 'mat-armatur-a1', name: 'Armatur A1', unit: 'ton', quantity: 4.2, usedQuantity: 2.6, remainingQuantity: 1.6, linkedStageId: 'stage-bunovre' },
  { id: 'mat-taxta', name: 'Taxta', unit: 'm3', quantity: 19, usedQuantity: 11, remainingQuantity: 8, linkedStageId: 'stage-dam' },
  { id: 'mat-dikt', name: 'Dikt', unit: 'ədəd', quantity: 280, usedQuantity: 160, remainingQuantity: 120, linkedStageId: 'stage-floor-1' },
  { id: 'mat-beton-b75', name: 'Beton B7.5', unit: 'm3', quantity: 18.3, usedQuantity: 18.3, remainingQuantity: 0, linkedStageId: 'stage-bunovre' },
  { id: 'mat-beton-b25', name: 'Beton B25', unit: 'm3', quantity: 328.2, usedQuantity: 182, remainingQuantity: 146.2, linkedStageId: 'stage-floor-1' },
  { id: 'mat-cinqil', name: 'Çınqıl', unit: 'm3', quantity: 16.1, usedQuantity: 16.1, remainingQuantity: 0, linkedStageId: 'stage-torpaq' },
  { id: 'mat-pencere', name: 'Alüminyum pəncərə', unit: 'm2', quantity: 65, usedQuantity: 0, remainingQuantity: 65, linkedStageId: 'stage-qapi-pencere' },
  { id: 'mat-aqlay', name: 'Aqlay daşı', unit: 'm2', quantity: 515, usedQuantity: 0, remainingQuantity: 515, linkedStageId: 'stage-suvaq' },
  { id: 'mat-kubik', name: 'Kubik daşı', unit: 'm2', quantity: 1270, usedQuantity: 230, remainingQuantity: 1040, linkedStageId: 'stage-horgu' },
]

export const projectWorkerAssignments: WorkerAssignment[] = [
  { id: 'wa-1', workerName: 'İlham Əliyev', workerExternalId: '1', crewId: 'crew-monolit', role: 'Betonçu', plannedDailyHours: 8, activeWorkItemId: 'item-floor1-karkas' },
  { id: 'wa-2', workerName: 'Tahirə Məmmədova', workerExternalId: '2', crewId: 'crew-horgu', role: 'Hörgü ustası', plannedDailyHours: 8, activeWorkItemId: 'item-horgu' },
  { id: 'wa-3', workerName: 'Samir Qasımov', workerExternalId: '3', crewId: 'crew-suvaq', role: 'Suvaqçı', plannedDailyHours: 8, activeWorkItemId: 'item-suvaq' },
]

export const projectProgressSeed: ProjectProgressData = {
  summary: villaEstimateSummary,
  stages: projectStages,
  workItems: projectWorkItems,
  crews: projectCrews,
  workerAssignments: projectWorkerAssignments,
  materials: projectMaterials,
}
