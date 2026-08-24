import { create } from 'zustand'
import { apiRequest, ApiClientError } from '../../shared/api/client'
import { useProjectProgressStore } from '../projectProgress/projectProgressStore'
import { useProjectSelectionStore } from '../../stores/projectSelectionStore'
import { clearAuthToken, getAuthToken, setAuthToken } from './authToken'

export type LicenseStatus = 'Pending' | 'Active' | 'Expired' | 'Revoked'

export interface AuthUser {
  id: string
  tenantId: string
  fullName: string
  email: string
  role: 'Owner' | 'Admin' | 'Manager' | 'Supervisor' | 'ProcurementAgent' | 'User'
  status: 'Active' | 'Disabled'
}

export interface AuthTenant {
  id: string
  companyName: string
  code: string
  status: 'Active' | 'Suspended'
}

export interface TenantLicense {
  id: string
  tenantId: string
  plan: 'Trial' | 'Starter' | 'Business' | 'Enterprise' | 'Unlimited'
  status: LicenseStatus
  startsAt: string
  expiresAt?: string
  maxProjects?: number
  maxUsers?: number
  maxCameras?: number
}

interface AuthResponse {
  accessToken: string
  user: AuthUser
  tenant: AuthTenant
  license?: TenantLicense
}

interface MeResponse {
  user: AuthUser
  tenant: AuthTenant
  license?: TenantLicense
}

interface AuthState {
  token: string
  user?: AuthUser
  tenant?: AuthTenant
  license?: TenantLicense
  loading: boolean
  initialized: boolean
  error?: string
  isAuthenticated: boolean
  hasActiveLicense: boolean
  loadMe: () => Promise<void>
  login: (email: string, password: string) => Promise<void>
  register: (payload: { companyName: string; fullName: string; email: string; password: string }) => Promise<void>
  activateLicense: (licenseKey: string) => Promise<void>
  logout: () => Promise<void>
}

export const LOGIN_FAILED_MESSAGE = 'Giriş alınmadı. Email və ya şifrə yanlışdır.'
export const LICENSE_INVALID_TENANT_MESSAGE = 'Bu lisenziya açarı bu şirkət hesabı üçün nəzərdə tutulmayıb.'
export const LICENSE_INVALID_MESSAGE = 'Lisenziya açarı yanlışdır və ya aktiv deyil.'
export const LICENSE_ACTIVATION_FAILED_MESSAGE = 'Lisenziya aktivləşdirilə bilmədi. Yenidən yoxlayın.'

const normalizeError = (error: unknown) => {
  if (error instanceof ApiClientError) return error.message || error.details || 'Sorğu alınmadı'
  if (error instanceof Error) return error.message
  return 'Sorğu alınmadı'
}

const normalizeLoginError = (error: unknown) => {
  if (error instanceof ApiClientError) {
    const apiMessage = error.message?.trim()
    if (error.status === 401 || !apiMessage || apiMessage === '""') return LOGIN_FAILED_MESSAGE
    return apiMessage
  }

  if (error instanceof Error && error.message) return error.message
  return LOGIN_FAILED_MESSAGE
}

const normalizeLicenseError = (error: unknown) => {
  if (error instanceof ApiClientError) {
    const payload = `${error.message} ${error.details}`.toLowerCase()
    if (payload.includes('not valid for this tenant')) return LICENSE_INVALID_TENANT_MESSAGE
    if (payload.includes('cannot be activated') || payload.includes('revoked') || payload.includes('expired')) return LICENSE_INVALID_MESSAGE
    if (error.status === 400 || error.status === 404 || !error.message?.trim() || error.message.trim() === '""') return LICENSE_ACTIVATION_FAILED_MESSAGE
  }

  return LICENSE_ACTIVATION_FAILED_MESSAGE
}

const resetTenantScopedBrowserState = (previousTenantId: string | undefined, nextTenantId: string | undefined) => {
  if (previousTenantId && previousTenantId !== nextTenantId) {
    useProjectSelectionStore.getState().clearSelection()
  }
}

const applyAuthResponse = (response: AuthResponse | MeResponse, token?: string, previousTenantId?: string) => {
  const nextToken = token ?? getAuthToken()
  if ('accessToken' in response) setAuthToken(response.accessToken)
  resetTenantScopedBrowserState(previousTenantId, response.tenant.id)
  useProjectSelectionStore.getState().setTenantScope(response.tenant.id)
  useProjectProgressStore.getState().prepareWorkspaceForTenant(response.tenant.id, response.tenant.code, response.tenant.companyName)
  return {
    token: 'accessToken' in response ? response.accessToken : nextToken,
    user: response.user,
    tenant: response.tenant,
    license: response.license,
    isAuthenticated: true,
    hasActiveLicense: response.license?.status === 'Active',
    error: undefined,
  }
}

export const useAuthStore = create<AuthState>()((set, get) => ({
  token: getAuthToken(),
  loading: false,
  initialized: false,
  isAuthenticated: Boolean(getAuthToken()),
  hasActiveLicense: false,
  loadMe: async () => {
    const token = getAuthToken()
    if (!token) {
      set({ token: '', user: undefined, tenant: undefined, license: undefined, initialized: true, isAuthenticated: false, hasActiveLicense: false })
      return
    }

    set({ loading: true, error: undefined })
    try {
      const previousTenantId = get().tenant?.id
      const response = await apiRequest<MeResponse>('/api/auth/me')
      set({ ...applyAuthResponse(response, token, previousTenantId), loading: false, initialized: true })
    } catch (error) {
      clearAuthToken()
      set({ token: '', user: undefined, tenant: undefined, license: undefined, loading: false, initialized: true, isAuthenticated: false, hasActiveLicense: false, error: normalizeError(error) })
    }
  },
  login: async (email, password) => {
    set({ loading: true, error: undefined })
    try {
      const previousTenantId = get().tenant?.id
      const response = await apiRequest<AuthResponse>('/api/auth/login', {
        method: 'POST',
        body: JSON.stringify({ email, password }),
      })
      set({ ...applyAuthResponse(response, undefined, previousTenantId), loading: false, initialized: true })
    } catch (error) {
      set({ loading: false, error: normalizeLoginError(error) })
      throw error
    }
  },
  register: async (payload) => {
    set({ loading: true, error: undefined })
    try {
      const previousTenantId = get().tenant?.id
      const response = await apiRequest<AuthResponse>('/api/auth/register', {
        method: 'POST',
        body: JSON.stringify(payload),
      })
      set({ ...applyAuthResponse(response, undefined, previousTenantId), loading: false, initialized: true })
    } catch (error) {
      set({ loading: false, error: normalizeLicenseError(error) })
      throw error
    }
  },
  activateLicense: async (licenseKey) => {
    set({ loading: true, error: undefined })
    try {
      const license = await apiRequest<TenantLicense>('/api/licenses/activate', {
        method: 'POST',
        body: JSON.stringify({ licenseKey }),
      })
      set({ license, hasActiveLicense: license.status === 'Active', loading: false, error: undefined })
    } catch (error) {
      set({ loading: false, error: normalizeError(error) })
      throw error
    }
  },
  logout: async () => {
    try {
      if (get().token) await apiRequest('/api/auth/logout', { method: 'POST' })
    } catch {
      // Frontend token clear is the source of truth for this SPA-stage logout.
    } finally {
      clearAuthToken()
      useProjectSelectionStore.getState().clearSelection()
      useProjectSelectionStore.getState().setTenantScope(undefined)
      useProjectProgressStore.getState().prepareWorkspaceForTenant('anonymous', 'EMPTY', undefined)
      set({ token: '', user: undefined, tenant: undefined, license: undefined, initialized: true, isAuthenticated: false, hasActiveLicense: false })
    }
  },
}))
