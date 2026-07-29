interface WrappedAxisTickProps {
  x?: number
  y?: number
  payload?: {
    value?: unknown
  }
  maxCharsPerLine?: number
  maxLines?: number
  lineHeight?: number
  dy?: number
}

const ELLIPSIS = '…'

const trimWithEllipsis = (value: string, maxLength: number) => {
  if (value.length <= maxLength) return value
  if (maxLength <= 1) return ELLIPSIS
  return `${value.slice(0, maxLength - 1).trimEnd()}${ELLIPSIS}`
}

export const wrapAxisLabel = (value: string, maxCharsPerLine = 14, maxLines = 2) => {
  const normalized = value.replace(/\s+/g, ' ').trim()
  if (!normalized) return ['']

  const words = normalized.split(' ')
  const lines: string[] = []
  let currentLine = ''

  words.forEach((word) => {
    const candidate = currentLine ? `${currentLine} ${word}` : word
    if (candidate.length <= maxCharsPerLine) {
      currentLine = candidate
      return
    }

    if (currentLine) lines.push(currentLine)
    currentLine = word
  })

  if (currentLine) lines.push(currentLine)

  const normalizedLines = lines.map((line) => trimWithEllipsis(line, maxCharsPerLine))

  if (normalizedLines.length <= maxLines) return normalizedLines

  const visible = normalizedLines.slice(0, maxLines)
  visible[visible.length - 1] = trimWithEllipsis(visible[visible.length - 1], maxCharsPerLine)
  return visible
}

export const WrappedAxisTick = ({
  dy = 16,
  lineHeight = 16,
  maxCharsPerLine = 14,
  maxLines = 2,
  payload,
  x = 0,
  y = 0,
}: WrappedAxisTickProps) => {
  const label = String(payload?.value ?? '')
  const lines = wrapAxisLabel(label, maxCharsPerLine, maxLines)

  return (
    <g transform={`translate(${x},${y})`}>
      <title>{label}</title>
      <text className="chart-axis-tick-label" textAnchor="middle" x={0} y={0} dy={dy}>
        {lines.map((line, index) => (
          <tspan key={`${line}-${index}`} x={0} dy={index === 0 ? 0 : lineHeight}>
            {line}
          </tspan>
        ))}
      </text>
    </g>
  )
}
