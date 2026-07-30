import type { AppLanguage } from './index'
import { translateUiText } from './uiText'

type Translator = (key: string, fallback?: string) => string

export const translateStatus = (status: unknown, t: Translator) => {
  const value = String(status ?? '')
  if (!value) return t('common.none', 'Yoxdur')
  return t(`status.${value}`, translateUiText(value, 'en') !== value ? translateUiText(value, 'az') : value)
}

export const translateAttendanceMethod = (method: unknown, t: Translator) => {
  const value = String(method ?? '')
  if (!value) return t('common.none', 'Yoxdur')
  if (value.toLowerCase() === 'face') return t('attendance.method.face', 'Üz')
  return t(`attendance.method.${value}`, value)
}

export const translateAttendanceSource = (source: unknown, t: Translator) => {
  const value = String(source ?? '')
  if (!value) return t('common.none', 'Yoxdur')
  if (value === 'dahua_active_register') return 'Active Register'
  if (value === 'dahua_cgi_polling') return 'CGI polling'
  if (value === 'attendance_live_status') return t('attendance.source.liveStatus', 'Live status')
  return t(`attendance.source.${value}`, translateUiText(value, 'az'))
}

export const translateSecurityEventType = (eventType: unknown, t: Translator) => {
  const value = String(eventType ?? '')
  if (!value) return t('common.none', 'Yoxdur')
  if (value === 'UnknownFace') return t('security.event.unknownFace', 'Tanınmayan üz')
  if (['SuspiciousRecognition', 'IdentityMismatch', 'IdentityMappingConflict', 'ParserUncertainSmartEvent'].includes(value)) {
    return t('security.event.suspiciousRecognition', 'Şübhəli tanıma')
  }
  return t(`security.event.${value}`, value)
}

export const translateWorkerSource = translateAttendanceSource

export const translateDuration = (minutes: number, language: AppLanguage) => {
  const safeMinutes = Math.max(0, Math.floor(minutes))
  const hours = Math.floor(safeMinutes / 60)
  const rest = safeMinutes % 60

  if (hours === 0) {
    if (language === 'en') return `${rest} min`
    if (language === 'ru') return `${rest} мин`
    return `${rest} dəq`
  }

  if (language === 'en') return `${hours} h ${rest} min`
  if (language === 'ru') return `${hours} ч ${rest} мин`
  return `${hours} saat ${rest} dəq`
}

export const translateUnit = (unit: unknown) => String(unit ?? '')
