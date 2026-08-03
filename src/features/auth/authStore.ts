import { create } from 'zustand'
import { apiRequest, ApiClientError } from '../../shared/api/client'
import { useProjectProgressStore } from '../projectProgress/projectProgressStore'
import { clearAuthToken, getAuthToken, setAuthToken } from './authToken'

export type LicenseStatus = 'Pending' | 'Active' | 'Expired' | 'Revoked'

export interface AuthUser {
  id: string
  tenantId: string
  fullName: string
  email: string
  role: 'Owner' | 'Admin' | 'Manager' | 'User'
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

const normalizeError = (error: unknown) => {
  if (error instanceof ApiClientError) return error.message || error.details || 'Sorğu alınmadı'
  if (error instanceof Error) return error.message
  return 'Sorğu alınmadı'
}

const applyAuthResponse = (response: AuthResponse | MeResponse, token?: string) => {
  const nextToken = token ?? getAuthToken()
  if ('accessToken' in response) setAuthToken(response.accessToken)
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
      const response = await apiRequest<MeResponse>('/api/auth/me')
      set({ ...applyAuthResponse(response, token), loading: false, initialized: true })
    } catch (error) {
      clearAuthToken()
      set({ token: '', user: undefined, tenant: undefined, license: undefined, loading: false, initialized: true, isAuthenticated: false, hasActiveLicense: false, error: normalizeError(error) })
    }
  },
  login: async (email, password) => {
    set({ loading: true, error: undefined })
    try {
      const response = await apiRequest<AuthResponse>('/api/auth/login', {
        method: 'POST',
        body: JSON.stringify({ email, password }),
      })
      set({ ...applyAuthResponse(response), loading: false, initialized: true })
    } catch (error) {
      set({ loading: false, error: normalizeError(error) })
      throw error
    }
  },
  register: async (payload) => {
    set({ loading: true, error: undefined })
    try {
      const response = await apiRequest<AuthResponse>('/api/auth/register', {
        method: 'POST',
        body: JSON.stringify(payload),
      })
      set({ ...applyAuthResponse(response), loading: false, initialized: true })
    } catch (error) {
      set({ loading: false, error: normalizeError(error) })
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
      set({ token: '', user: undefined, tenant: undefined, license: undefined, initialized: true, isAuthenticated: false, hasActiveLicense: false })
    }
  },
}))
