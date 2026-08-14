import { authHeader } from '../../features/auth/authToken'

export const API_BASE_URL = (
  (import.meta.env.VITE_API_BASE_URL as string | undefined)
  ?? 'http://46.101.182.202:8080'
).replace(/\/$/, '')

export const API_BASE_URL_SOURCE = (import.meta.env.VITE_API_BASE_URL as string | undefined)
  ? 'VITE_API_BASE_URL'
  : 'default-dev-url'

export class ApiClientError extends Error {
  readonly url: string
  readonly status: number
  readonly details: string

  constructor(
    message: string,
    url: string,
    status: number,
    details: string,
  ) {
    super(message)
    this.name = 'ApiClientError'
    this.url = url
    this.status = status
    this.details = details
  }
}

const parseBody = (text: string) => {
  if (!text) return undefined
  try {
    return JSON.parse(text) as unknown
  } catch {
    return text
  }
}

const extractErrorMessage = (parsed: unknown, status: number) => {
  if (typeof parsed === 'string') {
    const message = parsed.trim()
    return message || `HTTP ${status} - server boş xəta cavabı qaytardı.`
  }

  if (parsed && typeof parsed === 'object') {
    const record = parsed as Record<string, unknown>
    for (const key of ['error', 'message', 'title']) {
      const value = record[key]
      if (typeof value === 'string' && value.trim()) return value.trim()
    }
    return JSON.stringify(parsed)
  }

  return `HTTP ${status} - server boş xəta cavabı qaytardı.`
}

export const apiRequest = async <T>(path: string, init?: RequestInit): Promise<T> => {
  const url = `${API_BASE_URL}${path}`
  const headers = new Headers(init?.headers)
  headers.set('Content-Type', headers.get('Content-Type') ?? 'application/json')
  Object.entries(authHeader()).forEach(([key, value]) => headers.set(key, value))
  const response = await fetch(url, {
    ...init,
    headers,
  })
  const text = response.status === 204 ? '' : await response.text()
  const parsed = parseBody(text)

  if (!response.ok) {
    console.warn('BuildTrack API request failed', { url, status: response.status, parsed })
    throw new ApiClientError(extractErrorMessage(parsed, response.status), url, response.status, text)
  }

  return parsed as T
}

export const tryApiRequest = async <T>(path: string, init?: RequestInit): Promise<T | undefined> => {
  try {
    return await apiRequest<T>(path, init)
  } catch (error) {
    console.debug('BuildTrack API request unavailable', { path, error })
    return undefined
  }
}
