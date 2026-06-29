import { Tag } from 'antd'
import type { AttendanceStatus, ExportStatus } from '../../types/models'

type StatusValue = AttendanceStatus | ExportStatus | 'Aktiv' | 'Qaralama' | 'Uyğun' | 'Yaxşı' | 'Nəzarət tələb edir' | 'Riskli' | 'Orta' | 'Təkmilləşdirilməlidir'

const statusColor = (status: StatusValue) => {
  if (status === 'Gəlib' || status === 'Hazır' || status === 'Aktiv' || status === 'Uyğun' || status === 'Yaxşı') return 'green'
  if (status === 'Gəlməyib' || status === 'Xəta' || status === 'Riskli' || status === 'Təkmilləşdirilməlidir') return 'red'
  if (status === 'Gecikib' || status === 'Xəbərdarlıq' || status === 'Nəzarət tələb edir' || status === 'Orta') return 'orange'
  if (status === 'Erkən çıxıb' || status === 'Qaralama') return 'purple'
  return 'blue'
}

export const StatusBadge = ({ status }: { status: StatusValue }) => <Tag color={statusColor(status)}>{status}</Tag>
