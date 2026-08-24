export const AUTH_TOKEN_STORAGE_KEY = 'buildtrack.authToken'

const removeLegacyLocalStorageToken = () => {
  if (typeof window === 'undefined') return
  try {
    window.localStorage.removeItem(AUTH_TOKEN_STORAGE_KEY)
  } catch {
    // Ignore unavailable storage; sessionStorage remains the active credential source.
  }
}

export const getAuthToken = () => {
  if (typeof window === 'undefined') return ''
  removeLegacyLocalStorageToken()
  return window.sessionStorage.getItem(AUTH_TOKEN_STORAGE_KEY) ?? ''
}

export const setAuthToken = (token: string) => {
  if (typeof window === 'undefined') return
  removeLegacyLocalStorageToken()
  window.sessionStorage.setItem(AUTH_TOKEN_STORAGE_KEY, token)
}

export const clearAuthToken = () => {
  if (typeof window === 'undefined') return
  removeLegacyLocalStorageToken()
  window.sessionStorage.removeItem(AUTH_TOKEN_STORAGE_KEY)
}

export const authHeader = (): Record<string, string> => {
  const token = getAuthToken()
  return token ? { Authorization: `Bearer ${token}` } : {}
}
