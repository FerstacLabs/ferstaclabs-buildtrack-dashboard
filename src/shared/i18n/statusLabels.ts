const statusLabels: Record<string, string> = {
  Pending: 'Gözləyir',
  Online: 'Qoşulub',
  Offline: 'Ayrılıb',
  Error: 'Xəta',
  Open: 'Açıq',
  Closed: 'Bağlı',
  NotStarted: 'Başlamayıb',
  InProgress: 'İcradadır',
  Paused: 'Dayanıb',
  Completed: 'Tamamlanıb',
  Delayed: 'Gecikir',
  Ready: 'Hazır',
  Warning: 'Xəbərdarlıq',
  Sent: 'Göndərilib',
  Failed: 'Uğursuz',
  openai: 'OpenAI',
  'local-fallback': 'Lokal analiz',
}

export const formatStatusAz = (status?: string | null) =>
  status ? statusLabels[status] ?? status : '-'

export const formatSourceAz = (source?: string | null) =>
  source ? statusLabels[source] ?? source.replace(/_/g, ' ') : '-'

export const formatExportStatusAz = (status?: string | null) =>
  formatStatusAz(status)
