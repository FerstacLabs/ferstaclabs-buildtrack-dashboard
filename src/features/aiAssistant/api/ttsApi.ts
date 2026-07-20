const devLog = (message: string, details?: unknown) => {
  if (import.meta.env.DEV) console.debug(`[BuildTrack AI] ${message}`, details ?? '')
}

export const fetchTtsAudio = async (text: string): Promise<Blob> => {
  const apiBase = (import.meta.env.VITE_API_BASE_URL as string | undefined)?.replace(/\/$/, '') ?? ''
  const url = `${apiBase}/api/ai/tts`
  devLog('TTS request URL', url)

  const response = await fetch(url, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json; charset=utf-8' },
    body: JSON.stringify({
      text,
      language: 'az-AZ',
      voice: 'shimmer',
    }),
  })

  devLog('TTS response status', response.status)
  const contentType = response.headers.get('content-type') || ''
  devLog('TTS response content-type', contentType)

  if (!response.ok) {
    const errorText = await response.text().catch(() => '')
    throw new Error(`TTS request failed: ${response.status} ${errorText}`)
  }

  if (!contentType.toLowerCase().includes('audio')) {
    const textResponse = await response.text().catch(() => '')
    throw new Error(`TTS did not return audio. content-type=${contentType}; body=${textResponse}`)
  }

  const blob = await response.blob()
  devLog('TTS blob size', blob.size)
  return blob
}
