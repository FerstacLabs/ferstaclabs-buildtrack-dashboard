export const formatNumber = (value: number, digits = 0) =>
  new Intl.NumberFormat('az-AZ', {
    maximumFractionDigits: digits,
    minimumFractionDigits: digits,
  }).format(value)

export const formatCurrency = (value: number) =>
  `${formatNumber(value, value % 1 === 0 ? 0 : 2)} AZN`

export const formatPercent = (value: number, digits = 1) => `${formatNumber(value, digits)}%`

export const formatHours = (value: number, digits = 1) => `${formatNumber(value, digits)} saat`

export const compactName = (value: string, max = 18) =>
  value.length > max ? `${value.slice(0, max - 1)}...` : value

export const downloadBlob = (fileName: string, blob: Blob) => {
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = fileName
  document.body.appendChild(link)
  link.click()
  document.body.removeChild(link)
  URL.revokeObjectURL(url)
}
