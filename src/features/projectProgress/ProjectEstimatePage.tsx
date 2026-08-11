import { DeleteOutlined, DownloadOutlined, EditOutlined, PlusOutlined, UploadOutlined } from '@ant-design/icons'
import { Alert, Button, Card, DatePicker, Divider, Drawer, Form, Input, InputNumber, Modal, Select, Slider, Space, Table, Tag, Upload, message } from 'antd'
import type { TableColumnsType } from 'antd'
import dayjs from 'dayjs'
import type { Dayjs } from 'dayjs'
import { useCallback, useEffect, useMemo, useState } from 'react'
import { ProjectSelect } from '../../components/ProjectSelect'
import { PageTitle } from '../../components/ui/PageTitle'
import { useI18n } from '../../i18n'
import { buildTrackBackendApi, type FieldWarehouseCatalogItem, type WarehouseStockItem } from '../../services/api/buildTrackBackendApi'
import type { MaterialItem, ProjectEstimateSummary } from '../../types/projectProgress'
import type { ProjectWorkStatus, WorkItem } from '../../types/projectProgress'
import { formatCurrency, formatHours, formatNumber } from '../../utils/formatters'
import { UnitSelect } from './constructionUnits'
import {
  downloadEstimateTemplate,
  exportEstimateWorkbook,
  parseEstimateWorkbook,
  type EstimateImportSummary,
  type InvalidEstimateRow,
  type ParsedEstimateRow,
} from './estimateExcelService'
import { ALL_OBJECTS_ID, getCrewsByObject, getEstimateRowsByObject, getMaterialsByObject, getObjectName, getStagesByObject } from './projectSelectors'
import { calculateStageProgress, statusColor, statusLabel, useProjectProgressStore } from './projectProgressStore'
import { useProjectSelectionStore } from '../../stores/projectSelectionStore'

interface WorkItemFormValues {
  stageId: string
  name: string
  costCode?: string
  unit: string
  quantity: number
  unitPrice?: number
  completedQuantity?: number
  laborUnitPrice: number
  materials?: WorkItemMaterialFormValue[]
  plannedHours: number
  actualHours: number
  assignedCrewId?: string
  status: ProjectWorkStatus
  progressPercent: number
  plannedStartDate?: Dayjs
  plannedEndDate?: Dayjs
  notes?: string
}

interface WorkItemMaterialFormValue {
  id?: string
  catalogItemId?: string
  materialName?: string
  materialUnit?: string
  materialQuantity?: number
  materialUnitPrice?: number
}

interface ProjectObjectFormValues {
  name: string
  address?: string
  plannedStartDate?: Dayjs
  plannedEndDate?: Dayjs
  clientName?: string
  notes?: string
}

const workStatusValues = Object.keys(statusLabel) as ProjectWorkStatus[]

const normalizeName = (value: string) => value.trim().toLocaleLowerCase('az-AZ').replace(/\s+/g, ' ')

const sameOptionalCode = (left?: string, right?: string) => normalizeName(left ?? '') === normalizeName(right ?? '')

const toDayjs = (value?: string) => (value ? dayjs(value) : undefined)

const toDateString = (value?: Dayjs | string) => {
  if (!value) return undefined
  return typeof value === 'string' ? value : value.format('YYYY-MM-DD')
}

const formatDisplayDate = (value?: string) => (value ? dayjs(value).format('DD.MM.YYYY') : '—')

const stockText = (stock?: WarehouseStockItem) => {
  if (!stock) return 'Anbarda: yoxdur'
  return `Anbarda: ${formatNumber(stock.availableQuantity)} ${stock.unit}`
}

export const ProjectEstimatePage = () => {
  const { language, t } = useI18n()
  const store = useProjectProgressStore()
  const {
    addCrew,
    addMaterial,
    addObject,
    addStage,
    addWorkItem,
    crews,
    deleteMaterial,
    deleteStage,
    deleteWorkItem,
    estimateVersions,
    project,
    stages,
    summary,
    syncTenantSites,
    updateMaterial,
    updateStage,
    updateWorkItem,
  } = store
  const selectedObjectId = useProjectSelectionStore((state) => state.selectedProjectId)
  const scopedStages = getStagesByObject(store, selectedObjectId)
  const scopedWorkItems = getEstimateRowsByObject(store, selectedObjectId)
  const scopedCrews = getCrewsByObject(store, selectedObjectId)
  const scopedMaterials = getMaterialsByObject(store, selectedObjectId)
  const [itemForm] = Form.useForm<WorkItemFormValues>()
  const [stageForm] = Form.useForm<{ name: string; totalCost: number; plannedHours: number; plannedStartDate?: Dayjs; plannedEndDate?: Dayjs }>()
  const [projectForm] = Form.useForm<ProjectObjectFormValues>()
  const [editingItem, setEditingItem] = useState<WorkItem>()
  const [itemDrawerOpen, setItemDrawerOpen] = useState(false)
  const [stageModalOpen, setStageModalOpen] = useState(false)
  const [projectModalOpen, setProjectModalOpen] = useState(false)
  const [previewOpen, setPreviewOpen] = useState(false)
  const [previewSheetNames, setPreviewSheetNames] = useState<string[]>([])
  const [previewRows, setPreviewRows] = useState<unknown[][]>([])
  const [importSummary, setImportSummary] = useState<EstimateImportSummary>()
  const [catalogItems, setCatalogItems] = useState<FieldWarehouseCatalogItem[]>([])
  const [warehouseStock, setWarehouseStock] = useState<WarehouseStockItem[]>([])
  const [catalogLoading, setCatalogLoading] = useState(false)
  const [estimateTableRevision, setEstimateTableRevision] = useState(0)
  const [estimateTablePage, setEstimateTablePage] = useState(1)
  const [stageTablePage, setStageTablePage] = useState(1)

  const stageOptions = scopedStages.map((stage) => ({ value: stage.id, label: stage.name }))
  const crewOptions = scopedCrews.map((crew) => ({ value: crew.id, label: crew.name }))
  const stockByCatalogItemId = useMemo(() => new Map(warehouseStock.map((stock) => [stock.catalogItemId, stock])), [warehouseStock])
  const catalogById = useMemo(() => new Map(catalogItems.map((item) => [item.id, item])), [catalogItems])
  const materialOptions = useMemo(() => catalogItems
    .map((item) => {
      const stock = stockByCatalogItemId.get(item.id)
      return {
        value: item.id,
        label: (
          <Space direction="vertical" size={0}>
            <strong>{item.nameAz || item.name || item.nameEn || item.code}</strong>
            <span className="muted-text">{[item.code, item.category, stockText(stock)].filter(Boolean).join(' · ')}</span>
          </Space>
        ),
        searchText: [item.name, item.nameAz, item.nameRu, item.nameEn, item.code, item.category, item.subcategory, item.searchAliases].filter(Boolean).join(' ').toLocaleLowerCase('az-AZ'),
        hasStock: Number(stock?.availableQuantity ?? 0) > 0,
      }
    })
    .sort((left, right) => Number(right.hasStock) - Number(left.hasStock)),
    [catalogItems, stockByCatalogItemId])
  const stageNameById = useMemo(() => new Map(stages.map((stage) => [stage.id, stage.name])), [stages])
  const crewNameById = useMemo(() => new Map(crews.map((crew) => [crew.id, crew.name])), [crews])
  const statusText = useCallback((status: ProjectWorkStatus) => t(`status.${status}`, statusLabel[status]), [t])
  const statusOptions = useMemo(() => workStatusValues.map((value) => ({ value, label: statusText(value) })), [statusText])
  const selectedObjectName = getObjectName(store, selectedObjectId)
  const targetObjectId = selectedObjectId === ALL_OBJECTS_ID ? store.objects[0]?.id : selectedObjectId
  const scopedSummary = useMemo<ProjectEstimateSummary>(() => {
    const laborAmount = scopedWorkItems.reduce((sum, item) => sum + item.laborTotal, 0)
    const materialAmount = scopedWorkItems.reduce((sum, item) => sum + item.materialTotal, 0)
    const totalAmount = scopedWorkItems.reduce((sum, item) => sum + item.totalCost, 0)
    const hiddenCostAmount = selectedObjectId === ALL_OBJECTS_ID ? summary.hiddenCostAmount : 0
    return {
      totalAmount: Math.round((totalAmount + hiddenCostAmount) * 100) / 100,
      laborAmount: Math.round(laborAmount * 100) / 100,
      materialAmount: Math.round(materialAmount * 100) / 100,
      hiddenCostAmount,
      currency: summary.currency,
    }
  }, [scopedWorkItems, selectedObjectId, summary.currency, summary.hiddenCostAmount])

  const refreshEstimateTables = useCallback(() => {
    setEstimateTableRevision((revision) => revision + 1)
  }, [])

  useEffect(() => {
    setEstimateTablePage(1)
    setStageTablePage(1)
  }, [selectedObjectId])

  useEffect(() => {
    let cancelled = false
    const loadMaterialSources = async () => {
      setCatalogLoading(true)
      try {
        const [items, stock] = await Promise.all([
          buildTrackBackendApi.searchCatalogItems({ limit: 100 }),
          buildTrackBackendApi.getWarehouseStock(),
        ])
        if (!cancelled) {
          setCatalogItems(items)
          setWarehouseStock(stock)
        }
      } catch (error) {
        console.warn('Smeta material catalog/stock load failed', error)
        if (!cancelled) {
          setCatalogItems([])
          setWarehouseStock([])
        }
      } finally {
        if (!cancelled) setCatalogLoading(false)
      }
    }

    void loadMaterialSources()

    return () => {
      cancelled = true
    }
  }, [])

  const searchCatalog = async (query: string) => {
    try {
      const items = await buildTrackBackendApi.searchCatalogItems({ q: query, limit: 100 })
      setCatalogItems(items)
    } catch (error) {
      console.warn('Smeta material catalog search failed', error)
    }
  }

  const openItemDrawer = (item?: WorkItem) => {
    setEditingItem(item)
    const linkedMaterials = item ? scopedMaterials.filter((material) => material.linkedWorkItemId === item.id) : []
    const materialRows = linkedMaterials.length
      ? linkedMaterials.map((material) => ({
          id: material.id,
          catalogItemId: material.catalogItemId,
          materialName: material.name,
          materialUnit: material.unit,
          materialQuantity: material.quantity,
          materialUnitPrice: material.unitPrice ?? 0,
        }))
      : item && item.materialQuantity > 0
        ? [{
            materialName: 'Legacy material',
            materialUnit: item.materialUnit ?? 'ədəd',
            materialQuantity: item.materialQuantity,
            materialUnitPrice: item.materialUnitPrice,
          }]
        : []
    itemForm.setFieldsValue(item ? {
      ...item,
      plannedStartDate: toDayjs(item.plannedStartDate),
      plannedEndDate: toDayjs(item.plannedEndDate),
      materials: materialRows,
    } : {
      stageId: scopedStages[0]?.id,
      name: '',
      costCode: '',
      unit: 'iş',
      quantity: 1,
      unitPrice: 0,
      completedQuantity: 0,
      laborUnitPrice: 0,
      materials: [],
      plannedHours: 0,
      actualHours: 0,
      status: 'NotStarted',
      progressPercent: 0,
      plannedStartDate: undefined,
      plannedEndDate: undefined,
    })
    setItemDrawerOpen(true)
  }

  const saveWorkItem = (values: WorkItemFormValues) => {
    const materialRows = (values.materials ?? [])
      .map((material) => {
        const catalogItem = material.catalogItemId ? catalogById.get(material.catalogItemId) : undefined
        const name = (catalogItem?.nameAz || catalogItem?.name || material.materialName || '').trim()
        const quantity = Number(material.materialQuantity ?? 0)
        const unitPrice = Number(material.materialUnitPrice ?? 0)
        return {
          ...material,
          catalogItem,
          name,
          quantity,
          unit: catalogItem?.unit || material.materialUnit || 'ədəd',
          unitPrice,
        }
      })
      .filter((material) => material.name && Number.isFinite(material.quantity) && material.quantity > 0)
    const laborTotal = values.quantity * values.laborUnitPrice
    const materialTotal = Math.round(materialRows.reduce((sum, material) => sum + material.quantity * material.unitPrice, 0) * 100) / 100
    const progressPercent = values.quantity > 0 && typeof values.completedQuantity === 'number'
      ? Math.min(100, Math.round((values.completedQuantity / values.quantity) * 1000) / 10)
      : values.progressPercent
    const selectedStage = stages.find((stage) => stage.id === values.stageId)
    const objectId = editingItem?.objectId ?? selectedStage?.objectId ?? (selectedObjectId === ALL_OBJECTS_ID ? store.objects[0]?.id : selectedObjectId)
    const payload = {
      stageId: values.stageId,
      name: values.name,
      costCode: values.costCode,
      unit: values.unit,
      quantity: values.quantity,
      unitPrice: values.unitPrice,
      completedQuantity: values.completedQuantity,
      laborUnitPrice: values.laborUnitPrice,
      plannedHours: values.plannedHours,
      actualHours: values.actualHours,
      assignedCrewId: values.assignedCrewId,
      status: values.status,
      notes: values.notes,
      objectId,
      progressPercent,
      plannedStartDate: toDateString(values.plannedStartDate),
      plannedEndDate: toDateString(values.plannedEndDate),
      materialUnit: materialRows[0]?.unit,
      materialQuantity: materialRows.reduce((sum, material) => sum + material.quantity, 0),
      materialUnitPrice: materialRows[0]?.unitPrice ?? 0,
      laborTotal,
      materialTotal,
      totalCost: laborTotal + materialTotal,
      remainingHours: Math.max(0, values.plannedHours - values.actualHours),
    }
    const savedItemId = editingItem?.id ?? addWorkItem(payload)
    if (editingItem) updateWorkItem(editingItem.id, payload)

    const previousLinkedMaterials = editingItem ? scopedMaterials.filter((material) => material.linkedWorkItemId === savedItemId) : []
    const retainedMaterialIds = new Set<string>()

    materialRows.forEach((material) => {
      const materialPayload: Omit<MaterialItem, 'id' | 'remainingQuantity'> = {
        objectId,
        catalogItemId: material.catalogItemId,
        category: material.catalogItem?.category,
        name: material.name,
        unit: material.unit,
        quantity: material.quantity,
        usedQuantity: Math.min(material.quantity, Math.max(0, material.quantity * (progressPercent / 100))),
        unitPrice: material.unitPrice,
        linkedStageId: values.stageId,
        linkedWorkItemId: savedItemId,
        notes: values.notes,
      }
      const existingMaterial = material.id
        ? previousLinkedMaterials.find((entry) => entry.id === material.id)
        : previousLinkedMaterials.find((entry) =>
            entry.catalogItemId === material.catalogItemId
            || (!entry.catalogItemId && normalizeName(entry.name) === normalizeName(material.name)))

      if (existingMaterial) {
        updateMaterial(existingMaterial.id, materialPayload)
        retainedMaterialIds.add(existingMaterial.id)
      } else {
        addMaterial(materialPayload)
      }
    })

    previousLinkedMaterials
      .filter((material) => !retainedMaterialIds.has(material.id) && !materialRows.some((row) => row.id === material.id))
      .forEach((material) => deleteMaterial(material.id))

    setItemDrawerOpen(false)
    setEditingItem(undefined)
    refreshEstimateTables()
    void message.success(materialRows.length ? 'Smeta sətri və bağlı materiallar yadda saxlandı' : 'Smeta sətri yadda saxlandı')
  }

  const createProjectObject = async (values: ProjectObjectFormValues) => {
    const name = values.name.trim()
    const address = values.address?.trim() ?? ''

    try {
      const site = await buildTrackBackendApi.createSite({
        name,
        address,
        timeZone: 'Asia/Baku',
      })
      syncTenantSites([site], 'merge')
      useProjectSelectionStore.getState().setSelectedProjectId(site.id)
      projectForm.resetFields()
      setProjectModalOpen(false)
      void message.success('Yeni layihə yaradıldı və bütün modullarda obyekt kimi göründü')
      return
    } catch (error) {
      console.warn('Backend site creation failed; local project object fallback will be used', error)
    }

    const objectId = addObject({
      name,
      address: values.address?.trim(),
      zone: values.address?.trim() || 'Yeni obyekt',
      plannedStartDate: toDateString(values.plannedStartDate),
      plannedEndDate: toDateString(values.plannedEndDate),
      clientName: values.clientName?.trim(),
      notes: values.notes?.trim(),
      status: 'NotStarted',
    })

    projectForm.resetFields()
    setProjectModalOpen(false)
    void message.success('Yeni layihə yaradıldı və obyekt filterlərinə əlavə olundu')

    useProjectSelectionStore.getState().setSelectedProjectId(objectId)
  }

  const addNewStage = (values: { name: string; totalCost: number; plannedHours: number; plannedStartDate?: Dayjs; plannedEndDate?: Dayjs }) => {
    addStage({
      name: values.name,
      objectId: selectedObjectId === ALL_OBJECTS_ID ? store.objects[0]?.id : selectedObjectId,
      totalCost: values.totalCost,
      laborCost: 0,
      materialCost: values.totalCost,
      plannedStartDate: toDateString(values.plannedStartDate) || '2026-10-01',
      plannedEndDate: toDateString(values.plannedEndDate) || '2026-10-15',
      status: 'NotStarted',
      progressPercent: 0,
      plannedHours: values.plannedHours,
      actualHours: 0,
    })
    setStageModalOpen(false)
    stageForm.resetFields()
    refreshEstimateTables()
    void message.success('Yeni etap əlavə edildi')
  }

  const applyImportedRows = (rows: ParsedEstimateRow[], invalidRows: InvalidEstimateRow[]): EstimateImportSummary => {
    if (!targetObjectId) {
      return {
        importedRows: 0,
        createdStages: 0,
        createdCrews: 0,
        createdMaterials: 0,
        skippedRows: rows.length + invalidRows.length,
        invalidRows: [...invalidRows, { rowNumber: 0, reason: 'Aktiv obyekt tapılmadı' }],
      }
    }

    const affectedStageIds = new Set<string>()
    const skippedRows = [...invalidRows]
    let importedRows = 0
    let createdStages = 0
    let createdCrews = 0
    let createdMaterials = 0

    rows.forEach((row) => {
      const findStage = () => useProjectProgressStore.getState().stages.find((stage) =>
        stage.objectId === targetObjectId && normalizeName(stage.name) === normalizeName(row.stageName))

      let stage = findStage()
      if (!stage) {
        addStage({
          objectId: targetObjectId,
          name: row.stageName,
          totalCost: row.totalCost,
          laborCost: row.laborTotal,
          materialCost: row.materialTotal,
          plannedStartDate: row.plannedStartDate ?? '2026-10-01',
          plannedEndDate: row.plannedEndDate ?? '2026-10-15',
          status: row.status,
          progressPercent: row.progressPercent,
          plannedHours: row.plannedHours,
          actualHours: row.actualHours,
          notes: row.notes,
        })
        stage = findStage()
        createdStages += 1
      }

      if (!stage) {
        skippedRows.push({ rowNumber: row.rowNumber, reason: 'Etap yaradıla bilmədi' })
        return
      }

      let assignedCrewId: string | undefined
      if (row.crewName) {
        const findCrew = () => useProjectProgressStore.getState().crews.find((crew) =>
          crew.objectId === targetObjectId && normalizeName(crew.name) === normalizeName(row.crewName ?? ''))
        let crew = findCrew()
        if (!crew) {
          addCrew({
            objectId: targetObjectId,
            name: row.crewName,
            type: 'Smeta',
            foremanName: 'Təyin edilməyib',
            workerCount: 0,
            activeWorkStageId: stage.id,
            plannedDailyHours: 8,
            status: row.status,
            progressPercent: row.progressPercent,
            notes: 'Smeta importu ilə yaradılıb',
          })
          crew = findCrew()
          createdCrews += 1
        }
        assignedCrewId = crew?.id
      }

      const currentState = useProjectProgressStore.getState()
      const existingItem = currentState.workItems.find((item) =>
        item.objectId === targetObjectId
        && item.stageId === stage.id
        && normalizeName(item.name) === normalizeName(row.workName)
        && sameOptionalCode(item.costCode, row.costCode))

      const workItemPayload = {
        objectId: targetObjectId,
        stageId: stage.id,
        name: row.workName,
        costCode: row.costCode,
        unit: row.unit,
        quantity: row.quantity,
        unitPrice: row.quantity > 0 ? row.totalCost / row.quantity : row.totalCost,
        completedQuantity: row.completedQuantity,
        laborUnitPrice: row.laborUnitPrice,
        laborTotal: row.laborTotal,
        materialUnit: row.materialUnit,
        materialQuantity: row.materialQuantity,
        materialUnitPrice: row.materialUnitPrice,
        materialTotal: row.materialTotal,
        totalCost: row.totalCost,
        plannedHours: row.plannedHours,
        actualHours: row.actualHours,
        remainingHours: Math.max(0, row.plannedHours - row.actualHours),
        assignedCrewId,
        status: row.status,
        progressPercent: row.progressPercent,
        plannedStartDate: row.plannedStartDate,
        plannedEndDate: row.plannedEndDate,
        notes: row.notes,
      }

      const workItemId = existingItem?.id ?? addWorkItem(workItemPayload)
      if (existingItem) updateWorkItem(existingItem.id, workItemPayload)

      if (row.materialName && row.materialQuantity > 0) {
        const materialPayload = {
          objectId: targetObjectId,
          name: row.materialName,
          unit: row.materialUnit,
          quantity: row.materialQuantity,
          usedQuantity: Math.min(row.materialQuantity, Math.max(0, row.materialQuantity * (row.progressPercent / 100))),
          unitPrice: row.materialUnitPrice,
          linkedStageId: stage.id,
          linkedWorkItemId: workItemId,
          notes: row.notes,
        }
        const latestState = useProjectProgressStore.getState()
        const existingMaterial = latestState.materials.find((material) =>
          material.objectId === targetObjectId
          && (material.linkedWorkItemId === workItemId
            || (material.linkedStageId === stage.id && normalizeName(material.name) === normalizeName(row.materialName ?? ''))))

        if (existingMaterial) updateMaterial(existingMaterial.id, materialPayload)
        else {
          addMaterial(materialPayload)
          createdMaterials += 1
        }
      }

      affectedStageIds.add(stage.id)
      importedRows += 1
    })

    const latestState = useProjectProgressStore.getState()
    affectedStageIds.forEach((stageId) => {
      const stage = latestState.stages.find((entry) => entry.id === stageId)
      if (!stage) return
      const items = latestState.workItems.filter((item) => item.stageId === stageId)
      updateStage(stageId, {
        laborCost: Math.round(items.reduce((sum, item) => sum + item.laborTotal, 0) * 100) / 100,
        materialCost: Math.round(items.reduce((sum, item) => sum + item.materialTotal, 0) * 100) / 100,
        totalCost: Math.round(items.reduce((sum, item) => sum + item.totalCost, 0) * 100) / 100,
        plannedHours: items.reduce((sum, item) => sum + item.plannedHours, 0),
        actualHours: items.reduce((sum, item) => sum + item.actualHours, 0),
        progressPercent: calculateStageProgress(stage, latestState.workItems),
      })
    })

    return {
      importedRows,
      createdStages,
      createdCrews,
      createdMaterials,
      skippedRows: skippedRows.length,
      invalidRows: skippedRows,
    }
  }

  const parseWorkbook = async (file: File) => {
    if (selectedObjectId === ALL_OBJECTS_ID) {
      void message.warning('Import üçün konkret layihə seçin')
      return
    }

    try {
      const result = await parseEstimateWorkbook(file)
      const nextSummary = applyImportedRows(result.rows, result.invalidRows)
      setPreviewSheetNames(result.sheetNames)
      setPreviewRows(result.previewRows)
      setImportSummary(nextSummary)
      setPreviewOpen(true)
      refreshEstimateTables()
      void message.success(t('estimate.importSuccess'))
    } catch (error) {
      console.error('Smeta import failed', error)
      void message.error('Smeta faylı oxunmadı')
    }
  }

  const exportEstimate = () => {
    exportEstimateWorkbook({
      projectName: project.name,
      objectName: selectedObjectName,
      estimateVersionName: estimateVersions.find((version) => version.id === project.activeEstimateVersionId)?.name ?? 'Cari smeta',
      summary: scopedSummary,
      stages: scopedStages,
      workItems: scopedWorkItems,
      crews: scopedCrews,
      materials: scopedMaterials,
      language,
      stageNameById,
      statusText,
    })
    void message.success(t('estimate.exported'))
  }

  const downloadTemplate = () => {
    downloadEstimateTemplate(language)
    void message.success(t('estimate.templateDownloaded'))
  }

  const columns: TableColumnsType<WorkItem> = [
    { title: 'Etap', dataIndex: 'stageId', width: 210, render: (value) => stageNameById.get(String(value)) ?? value, filters: scopedStages.map((stage) => ({ text: stage.name, value: stage.id })), onFilter: (value, record) => record.stageId === value },
    { title: 'İş adı', dataIndex: 'name', width: 250, render: (value, row) => <strong>{value}<br /><span className="muted-text">{row.costCode ?? 'Cost code yoxdur'}</span></strong> },
    { title: t('estimate.workUnit'), dataIndex: 'unit', width: 110 },
    { title: 'Miqdar', dataIndex: 'quantity', width: 100, align: 'right', sorter: (a, b) => a.quantity - b.quantity },
    { title: 'Tamamlanan', dataIndex: 'completedQuantity', width: 120, align: 'right', render: (value, row) => `${formatNumber(Number(value ?? 0), 1)} / ${formatNumber(row.quantity, 1)}` },
    { title: 'İşçilik', dataIndex: 'laborTotal', width: 130, align: 'right', render: (value) => formatCurrency(Number(value)), sorter: (a, b) => a.laborTotal - b.laborTotal },
    { title: 'Material', dataIndex: 'materialTotal', width: 130, align: 'right', render: (value) => formatCurrency(Number(value)), sorter: (a, b) => a.materialTotal - b.materialTotal },
    { title: 'Ümumi xərc', dataIndex: 'totalCost', width: 130, align: 'right', render: (value) => formatCurrency(Number(value)), sorter: (a, b) => a.totalCost - b.totalCost },
    { title: 'Plan saat', dataIndex: 'plannedHours', width: 120, align: 'right', render: (value) => formatHours(Number(value), 0) },
    { title: 'Faktiki saat', dataIndex: 'actualHours', width: 120, align: 'right', render: (value) => formatHours(Number(value), 0) },
    { title: 'Briqada', dataIndex: 'assignedCrewId', width: 170, render: (value) => crewNameById.get(String(value)) ?? 'Təyin edilməyib' },
    { title: 'Status', dataIndex: 'status', width: 130, render: (value: ProjectWorkStatus, row) => <Tag key={`${row.id}:${value}`} color={statusColor[value]}>{statusText(value)}</Tag> },
    { title: 'Gedişat %', dataIndex: 'progressPercent', width: 160, render: (value, row) => <Slider min={0} max={100} value={Number(value)} onChange={(progressPercent) => updateWorkItem(row.id, { progressPercent })} onChangeComplete={refreshEstimateTables} /> },
    {
      title: 'Əməliyyat',
      fixed: 'right',
      width: 110,
      render: (_, row) => (
        <Space>
          <Button icon={<EditOutlined />} onClick={() => openItemDrawer(row)} />
          <Button
            danger
            icon={<DeleteOutlined />}
            onClick={() => Modal.confirm({ title: 'Sətri silmək istəyirsiniz?', okText: 'Sil', cancelText: 'İmtina', onOk: () => { deleteWorkItem(row.id); refreshEstimateTables() } })}
          />
        </Space>
      ),
    },
  ]

  return (
    <div className="page-stack project-progress-page">
      <PageTitle
        title={t('estimate.title')}
        subtitle={`${project.name} ${t('estimate.subtitle')}`}
        extra={
          <Space wrap>
            <Button onClick={() => setProjectModalOpen(true)}>{t('estimate.newProject')}</Button>
            <ProjectSelect pageKey="estimate" />
            <Upload accept=".xlsx,.xls" showUploadList={false} beforeUpload={(file) => { void parseWorkbook(file as File); return false }}>
              <Button icon={<UploadOutlined />}>{t('estimate.import')}</Button>
            </Upload>
            <Button icon={<DownloadOutlined />} onClick={downloadTemplate}>{t('estimate.template')}</Button>
            <Button icon={<PlusOutlined />} onClick={() => setStageModalOpen(true)}>{t('estimate.newStage')}</Button>
            <Button type="primary" icon={<PlusOutlined />} onClick={() => openItemDrawer()}>{t('estimate.newWorkItem')}</Button>
            <Button icon={<DownloadOutlined />} onClick={exportEstimate}>{t('estimate.export')}</Button>
          </Space>
        }
      />

      <section className="table-card">
        <div className="card-heading">
          <h2>{t('estimate.current')}</h2>
          <Space wrap>
            {estimateVersions.map((version) => <Tag color="blue" key={version.id}>{version.name}</Tag>)}
            <Button size="small" onClick={() => void message.success('Yeni smeta versiyası üçün hazır struktur yaradılıb')}>Yeni versiya yarat</Button>
          </Space>
        </div>
        <Table
          key={`estimate:${selectedObjectId}:${estimateTableRevision}`}
          rowKey="id"
          columns={columns}
          dataSource={scopedWorkItems}
          pagination={{ pageSize: 8, current: estimateTablePage, onChange: setEstimateTablePage }}
          scroll={{ x: 1640 }}
        />
      </section>

      <section className="table-card">
        <div className="card-heading">
          <h2>Etaplar</h2>
        </div>
        <Table
          key={`stages:${selectedObjectId}:${estimateTableRevision}`}
          rowKey="id"
          dataSource={scopedStages}
          pagination={{ pageSize: 6, current: stageTablePage, onChange: setStageTablePage }}
          columns={[
            { title: 'Sıra', dataIndex: 'order', width: 70 },
            { title: 'Etap', dataIndex: 'name' },
            { title: 'Məbləğ', dataIndex: 'totalCost', align: 'right', render: (value) => formatCurrency(Number(value)) },
            { title: 'Plan tarix', render: (_, row) => `${formatDisplayDate(row.plannedStartDate)} - ${formatDisplayDate(row.plannedEndDate)}` },
            { title: 'Status', dataIndex: 'status', render: (value: ProjectWorkStatus, row) => <Tag key={`${row.id}:${value}`} color={statusColor[value]}>{statusText(value)}</Tag> },
            { title: 'Əməliyyat', width: 120, render: (_, row) => <Button danger icon={<DeleteOutlined />} onClick={() => Modal.confirm({ title: 'Etapı və ona bağlı işləri silmək istəyirsiniz?', okText: 'Sil', cancelText: 'İmtina', onOk: () => { deleteStage(row.id); refreshEstimateTables() } })} /> },
          ]}
        />
      </section>

      <Drawer title={editingItem ? 'İş sətrini redaktə et' : 'Yeni iş sətri'} open={itemDrawerOpen} width={600} onClose={() => setItemDrawerOpen(false)}>
        <Form form={itemForm} layout="vertical" onFinish={saveWorkItem}>
          <Form.Item name="stageId" label="Etap" rules={[{ required: true }]}><Select showSearch options={stageOptions} /></Form.Item>
          <Form.Item name="name" label="İş adı" rules={[{ required: true }]}><Input /></Form.Item>
          <Space.Compact block>
            <Form.Item name="costCode" label="Cost Code" className="form-half"><Input /></Form.Item>
            <Form.Item name="unit" label={t('estimate.workUnit')} extra={t('estimate.workUnit.help')} rules={[{ required: true }]} className="form-half">
              <UnitSelect placeholder={t('estimate.workUnit.placeholder')} />
            </Form.Item>
          </Space.Compact>
          <Space.Compact block>
            <Form.Item name="quantity" label="Miqdar" rules={[{ required: true }]} className="form-half"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item>
            <Form.Item name="completedQuantity" label="Tamamlanan miqdar" className="form-half"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item>
          </Space.Compact>
          <Space.Compact block>
            <Form.Item name="laborUnitPrice" label="İşçilik vahid qiyməti" className="form-half"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item>
            <Form.Item name="plannedHours" label="Plan saat" className="form-half"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item>
          </Space.Compact>

          <Divider>Material istifadəsi</Divider>
          <Form.List name="materials">
            {(fields, { add, remove }) => (
              <Space direction="vertical" className="full-width">
                <Alert
                  showIcon
                  type="info"
                  message="Materiallar mərkəzi kataloqdan seçilir"
                  description="Bu seçim yalnız smeta planını yeniləyir; anbar qalığı azalmır və satınalma tapşırığı yaradılmır."
                />
                {fields.map((field) => {
                  const catalogItemId = itemForm.getFieldValue(['materials', field.name, 'catalogItemId']) as string | undefined
                  const catalogItem = catalogItemId ? catalogById.get(catalogItemId) : undefined
                  const stock = catalogItemId ? stockByCatalogItemId.get(catalogItemId) : undefined

                  return (
                    <Card size="small" key={field.key} title={`Material ${field.name + 1}`}>
                      <Form.Item name={[field.name, 'id']} hidden><Input /></Form.Item>
                      <Form.Item name={[field.name, 'materialName']} hidden><Input /></Form.Item>
                      <Form.Item name={[field.name, 'catalogItemId']} label="Material" rules={[{ required: true, message: 'Material seçin' }]}>
                        <Select
                          allowClear
                          showSearch
                          loading={catalogLoading}
                          placeholder="Kataloqda axtarın: beton, sement, armatur, kaska, kabel"
                          options={materialOptions}
                          optionFilterProp="searchText"
                          filterOption={(input, option) => String(option?.searchText ?? '').includes(input.toLocaleLowerCase('az-AZ'))}
                          onSearch={(query) => { if (query.trim().length >= 2) void searchCatalog(query.trim()) }}
                          onChange={(catalogId) => {
                            const item = catalogId ? catalogById.get(catalogId) : undefined
                            itemForm.setFieldValue(['materials', field.name, 'materialName'], item?.nameAz || item?.name || '')
                            itemForm.setFieldValue(['materials', field.name, 'materialUnit'], item?.unit || undefined)
                          }}
                        />
                      </Form.Item>
                      {catalogItem && (
                        <Alert
                          type="success"
                          showIcon
                          message={[catalogItem.code, catalogItem.category].filter(Boolean).join(' · ')}
                          description={stockText(stock)}
                          style={{ marginBottom: 12 }}
                        />
                      )}
                      <Space.Compact block>
                        <Form.Item name={[field.name, 'materialUnit']} label="Material ölçü vahidi" className="form-half">
                          <Input readOnly placeholder="Kataloqdan avtomatik" />
                        </Form.Item>
                        <Form.Item name={[field.name, 'materialQuantity']} label="Material miqdarı" rules={[{ type: 'number', min: 0.000001, message: 'Miqdar 0-dan böyük olmalıdır' }]} className="form-half">
                          <InputNumber min={0.000001} step={0.1} style={{ width: '100%' }} />
                        </Form.Item>
                      </Space.Compact>
                      <Space.Compact block>
                        <Form.Item name={[field.name, 'materialUnitPrice']} label="Plan vahid qiyməti" className="form-half">
                          <InputNumber min={0} step={0.01} style={{ width: '100%' }} />
                        </Form.Item>
                        <div className="form-half" style={{ display: 'flex', alignItems: 'center', justifyContent: 'flex-end' }}>
                          <Button danger icon={<DeleteOutlined />} onClick={() => remove(field.name)}>Sil</Button>
                        </div>
                      </Space.Compact>
                    </Card>
                  )
                })}
                <Button type="dashed" block icon={<PlusOutlined />} onClick={() => add({ materialQuantity: 1, materialUnitPrice: 0 })}>Material əlavə et</Button>
              </Space>
            )}
          </Form.List>

          <Space.Compact block>
            <Form.Item name="actualHours" label="Faktiki saat" className="form-half"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item>
            <Form.Item name="assignedCrewId" label="Briqada" className="form-half"><Select allowClear showSearch options={crewOptions} /></Form.Item>
          </Space.Compact>
          <Space.Compact block>
            <Form.Item name="plannedStartDate" label="Başlama tarixi" className="form-half"><DatePicker format="DD.MM.YYYY" allowClear style={{ width: '100%' }} onChange={(date) => { const end = itemForm.getFieldValue('plannedEndDate') as Dayjs | undefined; if (date && end?.isBefore(date, 'day')) itemForm.setFieldValue('plannedEndDate', undefined) }} /></Form.Item>
            <Form.Item name="plannedEndDate" label="Bitmə tarixi" className="form-half" rules={[({ getFieldValue }) => ({ validator: (_, value?: Dayjs) => { const startDate = getFieldValue('plannedStartDate') as Dayjs | undefined; if (!value || !startDate || !value.isBefore(startDate, 'day')) return Promise.resolve(); return Promise.reject(new Error('Bitmə tarixi başlama tarixindən əvvəl ola bilməz')) } })]}><DatePicker format="DD.MM.YYYY" allowClear style={{ width: '100%' }} disabledDate={(current) => { const startDate = itemForm.getFieldValue('plannedStartDate') as Dayjs | undefined; return Boolean(startDate && current && current.isBefore(startDate, 'day')) }} /></Form.Item>
          </Space.Compact>
          <Form.Item name="status" label="Status"><Select options={statusOptions} /></Form.Item>
          <Form.Item name="progressPercent" label="Gedişat %"><Slider min={0} max={100} /></Form.Item>
          <Form.Item name="notes" label="Qeyd"><Input.TextArea rows={3} /></Form.Item>
          <Button type="primary" htmlType="submit" block>Yadda saxla</Button>
        </Form>
      </Drawer>

      <Modal title="Yeni layihə / obyekt" open={projectModalOpen} onCancel={() => setProjectModalOpen(false)} onOk={() => projectForm.submit()} okText="Yarat" cancelText="İmtina">
        <Form form={projectForm} layout="vertical" onFinish={createProjectObject}>
          <Form.Item name="name" label="Layihə / obyekt adı" rules={[{ required: true, message: 'Obyekt adı yazın' }]}><Input placeholder="Məsələn: Villa B blok" /></Form.Item>
          <Form.Item name="address" label="Ünvan"><Input /></Form.Item>
          <Space.Compact block>
            <Form.Item name="plannedStartDate" label="Başlama tarixi" className="form-half"><DatePicker format="DD.MM.YYYY" allowClear style={{ width: '100%' }} onChange={(date) => { const end = projectForm.getFieldValue('plannedEndDate') as Dayjs | undefined; if (date && end?.isBefore(date, 'day')) projectForm.setFieldValue('plannedEndDate', undefined) }} /></Form.Item>
            <Form.Item name="plannedEndDate" label="Plan bitmə tarixi" className="form-half" rules={[({ getFieldValue }) => ({ validator: (_, value?: Dayjs) => { const startDate = getFieldValue('plannedStartDate') as Dayjs | undefined; if (!value || !startDate || !value.isBefore(startDate, 'day')) return Promise.resolve(); return Promise.reject(new Error('Bitmə tarixi başlama tarixindən əvvəl ola bilməz')) } })]}><DatePicker format="DD.MM.YYYY" allowClear style={{ width: '100%' }} disabledDate={(current) => { const startDate = projectForm.getFieldValue('plannedStartDate') as Dayjs | undefined; return Boolean(startDate && current && current.isBefore(startDate, 'day')) }} /></Form.Item>
          </Space.Compact>
          <Form.Item name="clientName" label="Müştəri / şirkət adı"><Input /></Form.Item>
          <Form.Item name="notes" label="Qeyd"><Input.TextArea rows={3} /></Form.Item>
        </Form>
      </Modal>

      <Modal title="Yeni etap" open={stageModalOpen} onCancel={() => setStageModalOpen(false)} onOk={() => stageForm.submit()} okText="Əlavə et" cancelText="İmtina">
        <Form form={stageForm} layout="vertical" onFinish={addNewStage}>
          <Form.Item name="name" label="Etap adı" rules={[{ required: true }]}><Input /></Form.Item>
          <Form.Item name="totalCost" label="Ümumi məbləğ"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item>
          <Form.Item name="plannedHours" label="Plan saat"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item>
          <Space.Compact block>
            <Form.Item name="plannedStartDate" label="Başlama" className="form-half"><DatePicker format="DD.MM.YYYY" allowClear style={{ width: '100%' }} onChange={(date) => { const end = stageForm.getFieldValue('plannedEndDate') as Dayjs | undefined; if (date && end?.isBefore(date, 'day')) stageForm.setFieldValue('plannedEndDate', undefined) }} /></Form.Item>
            <Form.Item name="plannedEndDate" label="Bitmə" className="form-half" rules={[({ getFieldValue }) => ({ validator: (_, value?: Dayjs) => { const startDate = getFieldValue('plannedStartDate') as Dayjs | undefined; if (!value || !startDate || !value.isBefore(startDate, 'day')) return Promise.resolve(); return Promise.reject(new Error('Bitmə tarixi başlama tarixindən əvvəl ola bilməz')) } })]}><DatePicker format="DD.MM.YYYY" allowClear style={{ width: '100%' }} disabledDate={(current) => { const startDate = stageForm.getFieldValue('plannedStartDate') as Dayjs | undefined; return Boolean(startDate && current && current.isBefore(startDate, 'day')) }} /></Form.Item>
          </Space.Compact>
        </Form>
      </Modal>

      <Modal title={t('estimate.importPreview')} open={previewOpen} onCancel={() => setPreviewOpen(false)} footer={<Button onClick={() => setPreviewOpen(false)}>Bağla</Button>} width={920}>
        <p>{t('estimate.importFoundSheets')}: {previewSheetNames.join(', ')}</p>
        {importSummary && (
          <Alert
            showIcon
            type={importSummary.skippedRows ? 'warning' : 'success'}
            message={t('estimate.importSummary')}
            description={
              <div>
                <Space wrap>
                  <Tag color="green">{t('estimate.importedRows')}: {importSummary.importedRows}</Tag>
                  <Tag color="blue">{t('estimate.createdStages')}: {importSummary.createdStages}</Tag>
                  <Tag color="purple">{t('estimate.createdMaterials')}: {importSummary.createdMaterials}</Tag>
                  <Tag color={importSummary.skippedRows ? 'orange' : 'default'}>{t('estimate.skippedRows')}: {importSummary.skippedRows}</Tag>
                </Space>
                {importSummary.invalidRows.length > 0 && (
                  <div className="muted-text" style={{ marginTop: 10 }}>
                    <strong>{t('estimate.invalidReasons')}:</strong>{' '}
                    {importSummary.invalidRows.slice(0, 5).map((row) => `#${row.rowNumber}: ${row.reason}`).join('; ')}
                  </div>
                )}
              </div>
            }
            style={{ marginBottom: 16 }}
          />
        )}
        <Table
          size="small"
          pagination={{ pageSize: 8 }}
          dataSource={previewRows.map((row, index) => ({ key: index, row }))}
          columns={[{ title: 'Sətir önizləmə', dataIndex: 'row', render: (row: unknown[]) => row.map((cell) => String(cell ?? '')).join(' | ') }]}
        />
      </Modal>
    </div>
  )
}
