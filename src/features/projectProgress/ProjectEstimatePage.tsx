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
import { projectProgressApi } from './projectProgressApi'
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

const WorkItemProgressSlider = ({ value, onCommit }: { value: number; onCommit: (value: number) => void }) => {
  const [draftValue, setDraftValue] = useState(Number(value))

  useEffect(() => {
    setDraftValue(Number(value))
  }, [value])

  return (
    <Slider
      min={0}
      max={100}
      value={draftValue}
      onChange={(nextValue) => setDraftValue(Number(nextValue))}
      onChangeComplete={(nextValue) => onCommit(Number(nextValue))}
    />
  )
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
    estimateVersions,
    loadFromBackend,
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
  const formatHoursText = useCallback((value: number, digits = 0) => {
    const safeValue = Number.isFinite(value) ? value : 0
    if (language === 'en') return `${formatNumber(safeValue, digits)} h`
    if (language === 'ru') return `${formatNumber(safeValue, digits)} ч`
    return formatHours(safeValue, digits)
  }, [language])
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

  const saveWorkItem = async (values: WorkItemFormValues) => {
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
    try {
      const savedItem = editingItem
        ? await projectProgressApi.updateWorkItem(editingItem.id, payload)
        : await projectProgressApi.createWorkItem(project.id, payload)
      const savedItemId = savedItem.id

      const previousLinkedMaterials = editingItem ? scopedMaterials.filter((material) => material.linkedWorkItemId === savedItemId) : []
      const retainedMaterialIds = new Set<string>()

      for (const material of materialRows) {
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
          await projectProgressApi.saveMaterial({
            ...existingMaterial,
            ...materialPayload,
            remainingQuantity: Math.max(0, materialPayload.quantity - materialPayload.usedQuantity),
          })
          retainedMaterialIds.add(existingMaterial.id)
        } else {
          await projectProgressApi.createMaterial(project.id, materialPayload)
        }
      }

      for (const material of previousLinkedMaterials.filter((material) => !retainedMaterialIds.has(material.id) && !materialRows.some((row) => row.id === material.id))) {
        await projectProgressApi.deleteMaterial(material.id)
      }

      await loadFromBackend()
      setItemDrawerOpen(false)
      setEditingItem(undefined)
      refreshEstimateTables()
      void message.success(materialRows.length ? t('estimate.workItemSavedWithMaterials') : t('estimate.workItemSaved'))
    } catch (error) {
      console.error('Project work item save failed', error)
      void message.error(t('estimate.workItemSaveFailed'))
    }
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
      await loadFromBackend()
      useProjectSelectionStore.getState().setSelectedProjectId(site.id)
      projectForm.resetFields()
      setProjectModalOpen(false)
      void message.success(t('estimate.projectCreatedBackend'))
      return
    } catch (error) {
      console.warn('Backend site creation failed; local project object fallback will be used', error)
    }

    const objectId = addObject({
      name,
      address: values.address?.trim(),
      zone: values.address?.trim() || t('estimate.newProject'),
      plannedStartDate: toDateString(values.plannedStartDate),
      plannedEndDate: toDateString(values.plannedEndDate),
      clientName: values.clientName?.trim(),
      notes: values.notes?.trim(),
      status: 'NotStarted',
    })

    projectForm.resetFields()
    setProjectModalOpen(false)
    void message.success(t('estimate.projectCreatedLocal'))

    useProjectSelectionStore.getState().setSelectedProjectId(objectId)
  }

  const addNewStage = async (values: { name: string; totalCost: number; plannedHours: number; plannedStartDate?: Dayjs; plannedEndDate?: Dayjs }) => {
    try {
      await projectProgressApi.createStage(project.id, {
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
      await loadFromBackend()
      setStageModalOpen(false)
      stageForm.resetFields()
      refreshEstimateTables()
      void message.success(t('estimate.stageSaved'))
    } catch (error) {
      console.error('Project stage save failed', error)
      void message.error(t('estimate.stageSaveFailed'))
    }
  }

  const saveWorkItemProgress = async (row: WorkItem, progressPercent: number) => {
    try {
      await projectProgressApi.updateWorkItem(row.id, { ...row, progressPercent })
      await loadFromBackend()
      refreshEstimateTables()
    } catch (error) {
      console.error('Project work item progress save failed', error)
      void message.error(t('estimate.progressSaveFailed'))
    }
  }

  const deleteServerWorkItem = async (row: WorkItem) => {
    try {
      await projectProgressApi.deleteWorkItem(row.id)
      await loadFromBackend()
      refreshEstimateTables()
      void message.success(t('estimate.workItemDeleted'))
    } catch (error) {
      console.error('Project work item delete failed', error)
      void message.error(t('estimate.workItemDeleteFailed'))
    }
  }

  const deleteServerStage = async (stageId: string) => {
    try {
      await projectProgressApi.deleteStage(stageId)
      await loadFromBackend()
      refreshEstimateTables()
      void message.success(t('estimate.stageDeleted'))
    } catch (error) {
      console.error('Project stage delete failed', error)
      void message.error(t('estimate.stageDeleteFailed'))
    }
  }

  const explainEstimateVersionCreation = () => {
    Modal.confirm({
      title: t('estimate.versionConfirmTitle'),
      width: 640,
      okText: t('estimate.versionConfirmOk'),
      cancelText: t('common.cancel'),
      content: (
        <Space direction="vertical" size={10}>
          <p>{t('estimate.versionConfirmIntro')}</p>
          <Alert
            type="info"
            showIcon
            message={t('estimate.versionCopiedTitle')}
            description={t('estimate.versionCopiedDescription')}
          />
          <Alert
            type="warning"
            showIcon
            message={t('estimate.versionResetTitle')}
            description={t('estimate.versionResetDescription')}
          />
          <p className="muted-text">{t('estimate.versionSafeNote')}</p>
        </Space>
      ),
      onOk: () => {
        void message.success(t('estimate.versionCreatedMessage'))
      },
    })
  }

  const applyImportedRows = (rows: ParsedEstimateRow[], invalidRows: InvalidEstimateRow[]): EstimateImportSummary => {
    if (!targetObjectId) {
      return {
        importedRows: 0,
        createdStages: 0,
        createdCrews: 0,
        createdMaterials: 0,
        skippedRows: rows.length + invalidRows.length,
        invalidRows: [...invalidRows, { rowNumber: 0, reason: t('estimate.activeProjectNotFound') }],
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
        skippedRows.push({ rowNumber: row.rowNumber, reason: t('estimate.stageCouldNotBeCreated') })
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
      void message.warning(t('estimate.selectProjectForImport'))
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
      void message.error(t('estimate.importFailed'))
    }
  }

  const exportEstimate = () => {
    exportEstimateWorkbook({
      projectName: project.name,
      objectName: selectedObjectName,
      estimateVersionName: estimateVersions.find((version) => version.id === project.activeEstimateVersionId)?.name ?? t('estimate.current'),
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
    { title: t('estimate.stage'), dataIndex: 'stageId', width: 210, render: (value) => stageNameById.get(String(value)) ?? value, filters: scopedStages.map((stage) => ({ text: stage.name, value: stage.id })), onFilter: (value, record) => record.stageId === value },
    { title: t('estimate.workName'), dataIndex: 'name', width: 250, render: (value, row) => <strong>{value}<br /><span className="muted-text">{row.costCode ?? t('estimate.noCostCode')}</span></strong> },
    { title: t('estimate.workUnit'), dataIndex: 'unit', width: 110 },
    { title: t('estimate.quantity'), dataIndex: 'quantity', width: 100, align: 'right', sorter: (a, b) => a.quantity - b.quantity },
    { title: t('estimate.completed'), dataIndex: 'completedQuantity', width: 120, align: 'right', render: (value, row) => `${formatNumber(Number(value ?? 0), 1)} / ${formatNumber(row.quantity, 1)}` },
    { title: t('estimate.labor'), dataIndex: 'laborTotal', width: 130, align: 'right', render: (value) => formatCurrency(Number(value)), sorter: (a, b) => a.laborTotal - b.laborTotal },
    { title: t('estimate.material'), dataIndex: 'materialTotal', width: 130, align: 'right', render: (value) => formatCurrency(Number(value)), sorter: (a, b) => a.materialTotal - b.materialTotal },
    { title: t('estimate.totalCost'), dataIndex: 'totalCost', width: 130, align: 'right', render: (value) => formatCurrency(Number(value)), sorter: (a, b) => a.totalCost - b.totalCost },
    { title: t('estimate.plannedHours'), dataIndex: 'plannedHours', width: 120, align: 'right', render: (value) => formatHoursText(Number(value), 0) },
    { title: t('estimate.actualHours'), dataIndex: 'actualHours', width: 120, align: 'right', render: (value) => formatHoursText(Number(value), 0) },
    { title: t('estimate.crew'), dataIndex: 'assignedCrewId', width: 170, render: (value) => crewNameById.get(String(value)) ?? t('common.notAssigned') },
    { title: t('common.status'), dataIndex: 'status', width: 130, render: (value: ProjectWorkStatus, row) => <Tag key={`${row.id}:${value}`} color={statusColor[value]}>{statusText(value)}</Tag> },
    { title: t('estimate.progressPercent'), dataIndex: 'progressPercent', width: 160, render: (value, row) => <WorkItemProgressSlider value={Number(value)} onCommit={(progressPercent) => void saveWorkItemProgress(row, progressPercent)} /> },
    {
      title: t('common.actions'),
      fixed: 'right',
      width: 110,
      render: (_, row) => (
        <Space>
          <Button icon={<EditOutlined />} onClick={() => openItemDrawer(row)} />
          <Button
            danger
            icon={<DeleteOutlined />}
            onClick={() => Modal.confirm({ title: t('estimate.deleteWorkItemConfirm'), okText: t('common.delete'), cancelText: t('common.cancel'), onOk: () => deleteServerWorkItem(row) })}
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
            <Button size="small" onClick={explainEstimateVersionCreation}>{t('estimate.createVersion')}</Button>
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
          <h2>{t('estimate.stages')}</h2>
        </div>
        <Table
          key={`stages:${selectedObjectId}:${estimateTableRevision}`}
          rowKey="id"
          dataSource={scopedStages}
          pagination={{ pageSize: 6, current: stageTablePage, onChange: setStageTablePage }}
          columns={[
            { title: t('estimate.order'), dataIndex: 'order', width: 70 },
            { title: t('estimate.stage'), dataIndex: 'name' },
            { title: t('estimate.amount'), dataIndex: 'totalCost', align: 'right', render: (value) => formatCurrency(Number(value)) },
            { title: t('estimate.plannedDate'), render: (_, row) => `${formatDisplayDate(row.plannedStartDate)} - ${formatDisplayDate(row.plannedEndDate)}` },
            { title: t('common.status'), dataIndex: 'status', render: (value: ProjectWorkStatus, row) => <Tag key={`${row.id}:${value}`} color={statusColor[value]}>{statusText(value)}</Tag> },
            { title: t('common.actions'), width: 120, render: (_, row) => <Button danger icon={<DeleteOutlined />} onClick={() => Modal.confirm({ title: t('estimate.deleteStageConfirm'), okText: t('common.delete'), cancelText: t('common.cancel'), onOk: () => deleteServerStage(row.id) })} /> },
          ]}
        />
      </section>

      <Drawer title={editingItem ? t('estimate.editWorkItem') : t('estimate.newWorkItem')} open={itemDrawerOpen} width={600} onClose={() => setItemDrawerOpen(false)}>
        <Form form={itemForm} layout="vertical" onFinish={saveWorkItem}>
          <Form.Item name="stageId" label={t('estimate.stage')} rules={[{ required: true }]}><Select showSearch options={stageOptions} /></Form.Item>
          <Form.Item name="name" label={t('estimate.workName')} rules={[{ required: true }]}><Input /></Form.Item>
          <Space.Compact block>
            <Form.Item name="costCode" label="Cost Code" className="form-half"><Input /></Form.Item>
            <Form.Item name="unit" label={t('estimate.workUnit')} extra={t('estimate.workUnit.help')} rules={[{ required: true }]} className="form-half">
              <UnitSelect placeholder={t('estimate.workUnit.placeholder')} />
            </Form.Item>
          </Space.Compact>
          <Space.Compact block>
            <Form.Item name="quantity" label={t('estimate.quantity')} rules={[{ required: true }]} className="form-half"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item>
            <Form.Item name="completedQuantity" label={t('estimate.completedQuantity')} className="form-half"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item>
          </Space.Compact>
          <Space.Compact block>
            <Form.Item name="laborUnitPrice" label={t('estimate.laborUnitPrice')} className="form-half"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item>
            <Form.Item name="plannedHours" label={t('estimate.plannedHours')} className="form-half"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item>
          </Space.Compact>

          <Divider>{t('estimate.materialUsage')}</Divider>
          <Form.List name="materials">
            {(fields, { add, remove }) => (
              <Space direction="vertical" className="full-width">
                <Alert
                  showIcon
                  type="info"
                  message={t('estimate.materialCatalogInfoTitle')}
                  description={t('estimate.materialCatalogInfoDescription')}
                />
                {fields.map((field) => {
                  const catalogItemId = itemForm.getFieldValue(['materials', field.name, 'catalogItemId']) as string | undefined
                  const catalogItem = catalogItemId ? catalogById.get(catalogItemId) : undefined
                  const stock = catalogItemId ? stockByCatalogItemId.get(catalogItemId) : undefined

                  return (
                    <Card size="small" key={field.key} title={`${t('estimate.material')} ${field.name + 1}`}>
                      <Form.Item name={[field.name, 'id']} hidden><Input /></Form.Item>
                      <Form.Item name={[field.name, 'materialName']} hidden><Input /></Form.Item>
                      <Form.Item name={[field.name, 'catalogItemId']} label={t('estimate.material')} rules={[{ required: true, message: t('estimate.selectMaterial') }]}>
                        <Select
                          allowClear
                          showSearch
                          loading={catalogLoading}
                          placeholder={t('estimate.materialSearchPlaceholder')}
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
                        <Form.Item name={[field.name, 'materialUnit']} label={t('estimate.materialUnit')} className="form-half">
                          <Input readOnly placeholder={t('estimate.catalogAuto')} />
                        </Form.Item>
                        <Form.Item name={[field.name, 'materialQuantity']} label={t('estimate.materialQuantity')} rules={[{ type: 'number', min: 0.000001, message: t('estimate.quantityPositive') }]} className="form-half">
                          <InputNumber min={0.000001} step={0.1} style={{ width: '100%' }} />
                        </Form.Item>
                      </Space.Compact>
                      <Space.Compact block>
                        <Form.Item name={[field.name, 'materialUnitPrice']} label={t('estimate.plannedUnitPrice')} className="form-half">
                          <InputNumber min={0} step={0.01} style={{ width: '100%' }} />
                        </Form.Item>
                        <div className="form-half" style={{ display: 'flex', alignItems: 'center', justifyContent: 'flex-end' }}>
                          <Button danger icon={<DeleteOutlined />} onClick={() => remove(field.name)}>{t('common.delete')}</Button>
                        </div>
                      </Space.Compact>
                    </Card>
                  )
                })}
                <Button type="dashed" block icon={<PlusOutlined />} onClick={() => add({ materialQuantity: 1, materialUnitPrice: 0 })}>{t('estimate.addMaterial')}</Button>
              </Space>
            )}
          </Form.List>

          <Space.Compact block>
            <Form.Item name="actualHours" label={t('estimate.actualHours')} className="form-half"><InputNumber min={0} style={{ width: '100%' }} /></Form.Item>
            <Form.Item name="assignedCrewId" label={t('estimate.crew')} className="form-half"><Select allowClear showSearch options={crewOptions} /></Form.Item>
          </Space.Compact>
          <Space.Compact block>
            <Form.Item name="plannedStartDate" label={t('estimate.startDate')} className="form-half"><DatePicker format="DD.MM.YYYY" allowClear style={{ width: '100%' }} onChange={(date) => { const end = itemForm.getFieldValue('plannedEndDate') as Dayjs | undefined; if (date && end?.isBefore(date, 'day')) itemForm.setFieldValue('plannedEndDate', undefined) }} /></Form.Item>
            <Form.Item name="plannedEndDate" label={t('estimate.endDate')} className="form-half" rules={[({ getFieldValue }) => ({ validator: (_, value?: Dayjs) => { const startDate = getFieldValue('plannedStartDate') as Dayjs | undefined; if (!value || !startDate || !value.isBefore(startDate, 'day')) return Promise.resolve(); return Promise.reject(new Error(t('estimate.endBeforeStart'))) } })]}><DatePicker format="DD.MM.YYYY" allowClear style={{ width: '100%' }} disabledDate={(current) => { const startDate = itemForm.getFieldValue('plannedStartDate') as Dayjs | undefined; return Boolean(startDate && current && current.isBefore(startDate, 'day')) }} /></Form.Item>
          </Space.Compact>
          <Form.Item name="status" label={t('common.status')}><Select options={statusOptions} /></Form.Item>
          <Form.Item name="progressPercent" label={t('estimate.progressPercent')}><Slider min={0} max={100} /></Form.Item>
          <Form.Item name="notes" label={t('common.note')}><Input.TextArea rows={3} /></Form.Item>
          <Button type="primary" htmlType="submit" block>{t('common.save')}</Button>
        </Form>
      </Drawer>

      <Modal title={t('estimate.newProject')} open={projectModalOpen} onCancel={() => setProjectModalOpen(false)} onOk={() => projectForm.submit()} okText={t('common.create')} cancelText={t('common.cancel')}>
        <Form form={projectForm} layout="vertical" onFinish={createProjectObject}>
          <Form.Item name="name" label={t('estimate.projectName')} rules={[{ required: true, message: t('estimate.enterProjectName') }]}><Input placeholder={t('estimate.projectNamePlaceholder')} /></Form.Item>
          <Form.Item name="address" label={t('common.address')}><Input /></Form.Item>
          <Space.Compact block>
            <Form.Item name="plannedStartDate" label={t('estimate.startDate')} className="form-half"><DatePicker format="DD.MM.YYYY" allowClear style={{ width: '100%' }} onChange={(date) => { const end = projectForm.getFieldValue('plannedEndDate') as Dayjs | undefined; if (date && end?.isBefore(date, 'day')) projectForm.setFieldValue('plannedEndDate', undefined) }} /></Form.Item>
            <Form.Item name="plannedEndDate" label={t('estimate.plannedEndDate')} className="form-half" rules={[({ getFieldValue }) => ({ validator: (_, value?: Dayjs) => { const startDate = getFieldValue('plannedStartDate') as Dayjs | undefined; if (!value || !startDate || !value.isBefore(startDate, 'day')) return Promise.resolve(); return Promise.reject(new Error(t('estimate.endBeforeStart'))) } })]}><DatePicker format="DD.MM.YYYY" allowClear style={{ width: '100%' }} disabledDate={(current) => { const startDate = projectForm.getFieldValue('plannedStartDate') as Dayjs | undefined; return Boolean(startDate && current && current.isBefore(startDate, 'day')) }} /></Form.Item>
          </Space.Compact>
          <Form.Item name="clientName" label={t('estimate.clientName')}><Input /></Form.Item>
          <Form.Item name="notes" label={t('common.note')}><Input.TextArea rows={3} /></Form.Item>
        </Form>
      </Modal>

      <Modal title={t('estimate.newStage')} open={stageModalOpen} onCancel={() => setStageModalOpen(false)} onOk={() => stageForm.submit()} okText={t('common.add')} cancelText={t('common.cancel')}>
        <Form form={stageForm} layout="vertical" onFinish={addNewStage}>
          <Form.Item name="name" label={t('estimate.stageName')} rules={[{ required: true }]}><Input /></Form.Item>
          <Form.Item name="totalCost" label={t('estimate.totalAmount')}><InputNumber min={0} style={{ width: '100%' }} /></Form.Item>
          <Form.Item name="plannedHours" label={t('estimate.plannedHours')}><InputNumber min={0} style={{ width: '100%' }} /></Form.Item>
          <Space.Compact block>
            <Form.Item name="plannedStartDate" label={t('estimate.start')} className="form-half"><DatePicker format="DD.MM.YYYY" allowClear style={{ width: '100%' }} onChange={(date) => { const end = stageForm.getFieldValue('plannedEndDate') as Dayjs | undefined; if (date && end?.isBefore(date, 'day')) stageForm.setFieldValue('plannedEndDate', undefined) }} /></Form.Item>
            <Form.Item name="plannedEndDate" label={t('estimate.end')} className="form-half" rules={[({ getFieldValue }) => ({ validator: (_, value?: Dayjs) => { const startDate = getFieldValue('plannedStartDate') as Dayjs | undefined; if (!value || !startDate || !value.isBefore(startDate, 'day')) return Promise.resolve(); return Promise.reject(new Error(t('estimate.endBeforeStart'))) } })]}><DatePicker format="DD.MM.YYYY" allowClear style={{ width: '100%' }} disabledDate={(current) => { const startDate = stageForm.getFieldValue('plannedStartDate') as Dayjs | undefined; return Boolean(startDate && current && current.isBefore(startDate, 'day')) }} /></Form.Item>
          </Space.Compact>
        </Form>
      </Modal>

      <Modal title={t('estimate.importPreview')} open={previewOpen} onCancel={() => setPreviewOpen(false)} footer={<Button onClick={() => setPreviewOpen(false)}>{t('common.close')}</Button>} width={920}>
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
          columns={[{ title: t('estimate.rowPreview'), dataIndex: 'row', render: (row: unknown[]) => row.map((cell) => String(cell ?? '')).join(' | ') }]}
        />
      </Modal>
    </div>
  )
}
