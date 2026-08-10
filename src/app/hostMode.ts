export type BuildTrackHostMode = 'Marketing' | 'ManagementApp' | 'FieldPortal' | 'SupplyPortal'

const MARKETING_HOSTS = new Set(['buildtrack.ferstaclabs.com'])
const FIELD_HOSTS = new Set(['field.buildtrack.ferstaclabs.com'])
const SUPPLY_HOSTS = new Set(['supply.buildtrack.ferstaclabs.com'])

export const getHostMode = (hostname = typeof window !== 'undefined' ? window.location.hostname : ''): BuildTrackHostMode => {
  const normalized = hostname.toLowerCase()
  if (FIELD_HOSTS.has(normalized)) return 'FieldPortal'
  if (SUPPLY_HOSTS.has(normalized)) return 'SupplyPortal'
  if (MARKETING_HOSTS.has(normalized)) return 'Marketing'
  return 'ManagementApp'
}

export const isFieldPortalHost = () => getHostMode() === 'FieldPortal'
export const isSupplyPortalHost = () => getHostMode() === 'SupplyPortal'

export const managementAppUrl = () => {
  const configured = import.meta.env.VITE_APP_BASE_URL as string | undefined
  return configured || 'https://app.buildtrack.ferstaclabs.com'
}

export const fieldPortalUrl = () => {
  const configured = import.meta.env.VITE_FIELD_BASE_URL as string | undefined
  return configured || 'https://field.buildtrack.ferstaclabs.com'
}

export const supplyPortalUrl = () => {
  const configured = import.meta.env.VITE_SUPPLY_BASE_URL as string | undefined
  return configured || 'https://supply.buildtrack.ferstaclabs.com'
}
