const devLog = (message: string, details?: unknown) => {
  if (import.meta.env.DEV) console.log(`[AI TTS] ${message}`, details ?? '')
}

export const fetchTtsAudio = async (text: string): Promise<Blob> => {
  const textToSpeak = text.trim().replace(/\s+/g, ' ').slice(0, 3900)
  if (!textToSpeak) throw new Error('TTS text is empty')

  const apiBase = ((import.meta.env.VITE_API_BASE_URL as string | undefined) || '').replace(/\/$/, '')
  const url = `${apiBase}/api/ai/tts`
  devLog('url', url)

  const response = await fetch(url, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json; charset=utf-8' },
    body: JSON.stringify({
      text: textToSpeak,
      language: 'az-AZ',
    }),
  })

  devLog('status', response.status)
  const contentType = response.headers.get('content-type') || ''
  devLog('content-type', contentType)

  if (!response.ok) {
    const errorText = await response.text().catch(() => '')
    throw new Error(`TTS failed: ${response.status} ${errorText}`)
  }

  if (!contentType.includes('audio')) {
    const bodyText = await response.text().catch(() => '')
    throw new Error(`TTS did not return audio. content-type=${contentType}; body=${bodyText}`)
  }

  const blob = await response.blob()
  devLog('blob size', blob.size)
  if (!blob || blob.size < 1000) {
    throw new Error(`TTS returned empty audio blob. size=${blob?.size}`)
  }

  return blob
}
