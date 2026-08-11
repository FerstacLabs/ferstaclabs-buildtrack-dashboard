import { Tag } from 'antd'
import {
  procurementNeedStatusLabel,
  procurementTaskLineStatusLabel,
  procurementTaskStatusLabel,
  warehouseLineStatusLabel,
  warehouseRequestStatusLabel,
} from '../../utils/warehouseWorkflowLabels'

const requestColor = (status?: string) => {
  if (status === 'ReadyForPickup') return 'green'
  if (status === 'InFulfillment' || status === 'PartiallyApproved' || status === 'NeedsJustification') return 'orange'
  if (status === 'Rejected' || status === 'Cancelled') return 'red'
  if (status === 'Issued' || status === 'Closed') return 'default'
  return 'blue'
}

const lineColor = (status?: string) => {
  if (status === 'Reserved' || status === 'ReadyForIssue' || status === 'Received') return 'green'
  if (status === 'NeedsProcurement' || status === 'ProcurementInProgress') return 'orange'
  if (status === 'Rejected') return 'red'
  if (status === 'Issued') return 'default'
  return 'blue'
}

const needColor = (status?: string) => {
  if (status === 'Received' || status === 'Purchased') return 'green'
  if (status === 'InPurchase' || status === 'PartiallyPurchased' || status === 'AwaitingReceipt') return 'orange'
  if (status === 'Cancelled') return 'red'
  return 'blue'
}

const taskColor = (status?: string) => {
  if (status === 'Completed' || status === 'Verified') return 'green'
  if (status === 'Shopping' || status === 'PartiallyCompleted' || status === 'SubmittedForVerification') return 'orange'
  if (status === 'RejectedForCorrection' || status === 'Cancelled') return 'red'
  return 'blue'
}

const StableStatusTag = ({
  status,
  label,
  color,
  prefix,
}: {
  status?: string
  label: string
  color: string
  prefix: string
}) => {
  const canonical = status?.trim() || 'Unknown'

  return (
    <Tag key={`${prefix}:${canonical}`} color={color} data-status={canonical}>
      <span key={`${prefix}:text:${canonical}`}>{label}</span>
    </Tag>
  )
}

export const WarehouseRequestStatusTag = ({ status }: { status?: string }) => (
  <StableStatusTag status={status} label={warehouseRequestStatusLabel(status)} color={requestColor(status)} prefix="warehouse-request-status" />
)

export const WarehouseLineStatusTag = ({ status }: { status?: string }) => (
  <StableStatusTag status={status} label={warehouseLineStatusLabel(status)} color={lineColor(status)} prefix="warehouse-line-status" />
)

export const ProcurementNeedStatusTag = ({ status }: { status?: string }) => (
  <StableStatusTag status={status} label={procurementNeedStatusLabel(status)} color={needColor(status)} prefix="procurement-need-status" />
)

export const ProcurementTaskStatusTag = ({ status }: { status?: string }) => (
  <StableStatusTag status={status} label={procurementTaskStatusLabel(status)} color={taskColor(status)} prefix="procurement-task-status" />
)

export const ProcurementTaskLineStatusTag = ({ status }: { status?: string }) => (
  <StableStatusTag status={status} label={procurementTaskLineStatusLabel(status)} color={taskColor(status)} prefix="procurement-task-line-status" />
)
