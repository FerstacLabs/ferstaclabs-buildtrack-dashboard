import type { RiskLevel } from '../types/models'

export const calculateRiskLevel = (score: number): RiskLevel => {
  if (score >= 80) return 'Kritik'
  if (score >= 60) return 'Yüksək'
  if (score >= 40) return 'Orta'
  return 'Aşağı'
}

export const riskColor = (level: RiskLevel) => {
  if (level === 'Kritik') return '#ef233c'
  if (level === 'Yüksək') return '#ff7a00'
  if (level === 'Orta') return '#f59e0b'
  return '#078b55'
}

export const riskTone = (score: number) => {
  if (score >= 80) return 'red'
  if (score >= 60) return 'orange'
  if (score >= 40) return 'orange'
  return 'green'
}

export const riskRecommendation = (score: number) => {
  if (score >= 80) return 'Araşdırılsın'
  if (score >= 60) return 'Xəbərdarlıq'
  if (score >= 40) return 'Nəzarət'
  return 'İzləmə davam etsin'
}
