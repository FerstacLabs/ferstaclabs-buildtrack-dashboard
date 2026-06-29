import { Tag } from 'antd'
import type { RiskLevel } from '../../types/models'
import { riskColor } from '../../utils/riskUtils'

export const RiskBadge = ({ level, score }: { level: RiskLevel; score?: number }) => (
  <Tag style={{ color: riskColor(level), borderColor: `${riskColor(level)}55`, background: `${riskColor(level)}12` }}>
    {score ? `${score} · ${level}` : level}
  </Tag>
)
