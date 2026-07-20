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

export const apiRequest = async <T>(path: string, init?: RequestInit): Promise<T> => {
  const url = `${API_BASE_URL}${path}`
  const response = await fetch(url, {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      ...init?.headers,
    },
  })
  const text = response.status === 204 ? '' : await response.text()
  const parsed = parseBody(text)

  if (!response.ok) {
    console.warn('BuildTrack API request failed', { url, status: response.status, parsed })
    throw new ApiClientError(typeof parsed === 'string' ? parsed : JSON.stringify(parsed ?? ''), url, response.status, text)
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
