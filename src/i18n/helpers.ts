import type { AppLanguage } from './index'
import { translateUiText } from './uiText'

export const languageChangedEventName = 'buildtrack-language-changed'

export const preferredLanguageForTenantCode = (tenantCode?: string): AppLanguage | undefined => {
  if (tenantCode?.trim().toUpperCase() === 'SKYSNAP-DEMO') return 'en'
  return undefined
}

export const applyPreferredTenantLanguage = (tenantCode?: string) => {
  const preferred = preferredLanguageForTenantCode(tenantCode)
  if (!preferred || typeof window === 'undefined') return
  window.localStorage.setItem('buildtrack-language', preferred)
  window.dispatchEvent(new CustomEvent(languageChangedEventName, { detail: preferred }))
}

type Translator = (key: string, fallback?: string) => string

export const crewTypeDefinitions = [
  { value: 'monolithic', labelKey: 'crewType.monolithic', legacy: ['Monolit'] },
  { value: 'concrete', labelKey: 'crewType.concrete', legacy: ['Betonçu'] },
  { value: 'rebar', labelKey: 'crewType.rebar', legacy: ['Armaturçu'] },
  { value: 'formwork', labelKey: 'crewType.formwork', legacy: ['Qəlibçi'] },
  { value: 'masonry', labelKey: 'crewType.masonry', legacy: ['Hörgü'] },
  { value: 'plastering', labelKey: 'crewType.plastering', legacy: ['Suvaq'] },
  { value: 'painting', labelKey: 'crewType.painting', legacy: ['Rəngsaz'] },
  { value: 'drywall', labelKey: 'crewType.drywall', legacy: ['Alçıpan'] },
  { value: 'tile', labelKey: 'crewType.tile', legacy: ['Kafel/metlax'] },
  { value: 'electrical', labelKey: 'crewType.electrical', legacy: ['Elektrik'] },
  { value: 'plumbing', labelKey: 'crewType.plumbing', legacy: ['Santexnik'] },
  { value: 'ventilation', labelKey: 'crewType.ventilation', legacy: ['Havalandırma'] },
  { value: 'heating', labelKey: 'crewType.heating', legacy: ['İsitmə sistemi'] },
  { value: 'roofing', labelKey: 'crewType.roofing', legacy: ['Dam örtüyü', 'Dam'] },
  { value: 'facade', labelKey: 'crewType.facade', legacy: ['Fasad'] },
  { value: 'windowsDoors', labelKey: 'crewType.windowsDoors', legacy: ['Pəncərə/qapı montajı', 'Pəncərə/Qapı'] },
  { value: 'metalStructures', labelKey: 'crewType.metalStructures', legacy: ['Metal konstruksiya'] },
  { value: 'welding', labelKey: 'crewType.welding', legacy: ['Qaynaq'] },
  { value: 'insulation', labelKey: 'crewType.insulation', legacy: ['İzolyasiya'] },
  { value: 'waterproofing', labelKey: 'crewType.waterproofing', legacy: ['Hidroizolyasiya'] },
  { value: 'earthworks', labelKey: 'crewType.earthworks', legacy: ['Torpaq işləri'] },
  { value: 'excavator', labelKey: 'crewType.excavator', legacy: ['Ekskavator'] },
  { value: 'roadworks', labelKey: 'crewType.roadworks', legacy: ['Yol işləri'] },
  { value: 'landscaping', labelKey: 'crewType.landscaping', legacy: ['Landşaft'] },
  { value: 'cleaning', labelKey: 'crewType.cleaning', legacy: ['Təmizlik'] },
  { value: 'materialsLogistics', labelKey: 'crewType.materialsLogistics', legacy: ['Material və logistika', 'Material/logistika'] },
  { value: 'safety', labelKey: 'crewType.safety', legacy: ['Təhlükəsizlik'] },
  { value: 'elevator', labelKey: 'crewType.elevator', legacy: ['Lift montajı'] },
  { value: 'fireSystem', labelKey: 'crewType.fireSystem', legacy: ['Yanğın sistemi'] },
  { value: 'cctvLowVoltage', labelKey: 'crewType.cctvLowVoltage', legacy: ['Kamera və zəif axın'] },
  { value: 'generalConstruction', labelKey: 'crewType.generalConstruction', legacy: ['Ümumi tikinti'] },
  { value: 'other', labelKey: 'crewType.other', legacy: ['Digər'] },
]

const normalizedCrewTypeMap = new Map(
  crewTypeDefinitions.flatMap((item) => [
    [item.value.toLocaleLowerCase('az-AZ'), item.value],
    ...item.legacy.map((legacy) => [legacy.toLocaleLowerCase('az-AZ'), item.value] as const),
  ]),
)

export const resolveCrewTypeValue = (typeValue: unknown) => {
  const value = String(typeValue ?? '').trim()
  if (!value) return 'other'
  return normalizedCrewTypeMap.get(value.toLocaleLowerCase('az-AZ')) ?? value
}

export const translateCrewType = (typeValue: unknown, t: Translator) => {
  const value = resolveCrewTypeValue(typeValue)
  const definition = crewTypeDefinitions.find((item) => item.value === value)
  return definition ? t(definition.labelKey, definition.legacy[0] ?? value) : value
}

export const crewTypeOptions = (t: Translator) =>
  crewTypeDefinitions.map((item) => ({ value: item.value, label: t(item.labelKey, item.legacy[0] ?? item.value) }))

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
