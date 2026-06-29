import dayjs from 'dayjs'

export const DEFAULT_START_DATE = '2025-05-01'
export const DEFAULT_END_DATE = '2025-05-31'
export const DEFAULT_MONTH = '2025-05'

export const toDisplayDate = (date: string) => dayjs(date).format('DD.MM.YYYY')

export const toDisplayDateTime = (date: string) => dayjs(date).format('DD.MM.YYYY HH:mm')

export const monthLabel = (month: string) => dayjs(`${month}-01`).format('MMM YYYY')

export const rangeLabel = (range: [string, string]) =>
  `${toDisplayDate(range[0])} - ${toDisplayDate(range[1])}`

export const isInDateRange = (date: string, range: [string, string]) => {
  const current = dayjs(date)
  return current.isSame(dayjs(range[0]), 'day') || current.isSame(dayjs(range[1]), 'day')
    ? true
    : current.isAfter(dayjs(range[0])) && current.isBefore(dayjs(range[1]))
}

export const getDatesInRange = (start: string, end: string) => {
  const dates: string[] = []
  let cursor = dayjs(start)
  const last = dayjs(end)

  while (cursor.isSame(last, 'day') || cursor.isBefore(last)) {
    dates.push(cursor.format('YYYY-MM-DD'))
    cursor = cursor.add(1, 'day')
  }

  return dates
}
