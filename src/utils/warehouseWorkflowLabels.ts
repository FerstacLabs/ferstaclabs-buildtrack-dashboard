export const priorityLabel = (priority?: string) => ({
  Normal: 'Normal',
  Urgent: 'Təcili',
  Critical: 'Kritik',
}[priority ?? ''] ?? priority ?? '-')

export const warehouseRequestStatusLabel = (status?: string) => ({
  Draft: 'Qaralama',
  Submitted: 'Göndərilib',
  UnderReview: 'Yoxlanılır',
  NeedsJustification: 'Əsaslandırma tələb olunur',
  PendingApproval: 'Təsdiq gözləyir',
  Approved: 'Təsdiqlənib',
  PartiallyApproved: 'Qismən təsdiqlənib',
  Rejected: 'Rədd edilib',
  InFulfillment: 'Təminat prosesində',
  ReadyForPickup: 'Təhvilə hazırdır',
  Issued: 'Verilib',
  Closed: 'Bağlanıb',
  Cancelled: 'Ləğv edilib',
}[status ?? ''] ?? status ?? 'Naməlum')

export const warehouseLineStatusLabel = (status?: string) => ({
  Pending: 'Gözləyir',
  StockAvailable: 'Anbarda mövcuddur',
  Reserved: 'Rezerv edilib',
  NeedsProcurement: 'Satınalma lazımdır',
  ProcurementInProgress: 'Satınalma prosesində',
  Received: 'Qəbul edilib',
  ReadyForIssue: 'Verilməyə hazırdır',
  Issued: 'Verilib',
  Rejected: 'Rədd edilib',
}[status ?? ''] ?? warehouseRequestStatusLabel(status))

export const procurementNeedStatusLabel = (status?: string) => ({
  PendingApproval: 'Təsdiq gözləyir',
  Approved: 'Təsdiqlənib',
  Assigned: 'Tapşırığa təyin edilib',
  InPurchase: 'Alış prosesində',
  PartiallyPurchased: 'Qismən alınıb',
  Purchased: 'Alınıb',
  AwaitingReceipt: 'Qəbul gözləyir',
  Received: 'Qəbul edilib',
  Cancelled: 'Ləğv edilib',
}[status ?? ''] ?? status ?? '-')

export const procurementTaskStatusLabel = (status?: string) => ({
  Draft: 'Qaralama',
  Assigned: 'Təyin edilib',
  Accepted: 'Qəbul edilib',
  Shopping: 'Alış prosesində',
  PartiallyCompleted: 'Qismən tamamlanıb',
  Completed: 'Tamamlanıb',
  SubmittedForVerification: 'Yoxlamaya göndərilib',
  Verified: 'Təsdiqlənib',
  RejectedForCorrection: 'Geri qaytarılıb',
  Cancelled: 'Ləğv edilib',
}[status ?? ''] ?? status ?? '-')

export const procurementTaskLineStatusLabel = (status?: string) => ({
  Pending: 'Gözləyir',
  Searching: 'Axtarılır',
  PartiallyPurchased: 'Qismən alınıb',
  Purchased: 'Alınıb',
  Unavailable: 'Tapılmadı',
  SubstitutionProposed: 'Əvəz təklif edilib',
  Received: 'Qəbul edilib',
  Rejected: 'Rədd edilib',
}[status ?? ''] ?? status ?? '-')

export const supplierStatusLabel = (status?: string) => ({
  Active: 'Aktiv',
  Proposed: 'Təklif olunub',
  Suspended: 'Dayandırılıb',
  Disabled: 'Deaktiv',
}[status ?? ''] ?? status ?? '-')

export const procurementAgentStatusLabel = (status?: string) => ({
  Active: 'Aktiv',
  Disabled: 'Deaktiv',
}[status ?? ''] ?? status ?? '-')

export const isTerminalWarehouseRequestStatus = (status?: string) =>
  status === 'Rejected' || status === 'Issued' || status === 'Closed' || status === 'Cancelled'
