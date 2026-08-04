import { useEffect, useRef, useState, type ImgHTMLAttributes, type ReactNode } from 'react'
import { authHeader } from '../../features/auth/authToken'

interface AuthenticatedSnapshotImageProps extends Omit<ImgHTMLAttributes<HTMLImageElement>, 'src' | 'onError'> {
  src?: string
  placeholder?: ReactNode
  onUnavailable?: () => void
}

export const AuthenticatedSnapshotImage = ({ src, placeholder = null, onUnavailable, ...imageProps }: AuthenticatedSnapshotImageProps) => {
  const [objectUrl, setObjectUrl] = useState('')
  const [failed, setFailed] = useState(false)
  const unavailableRef = useRef(onUnavailable)

  useEffect(() => {
    unavailableRef.current = onUnavailable
  }, [onUnavailable])

  useEffect(() => {
    if (!src) {
      setObjectUrl('')
      setFailed(true)
      return
    }

    const controller = new AbortController()
    let nextObjectUrl = ''
    setFailed(false)
    setObjectUrl('')

    const loadImage = async () => {
      try {
        const headers = new Headers()
        Object.entries(authHeader()).forEach(([key, value]) => headers.set(key, value))
        const response = await fetch(src, { headers, signal: controller.signal })
        const contentType = response.headers.get('content-type') ?? ''
        if (!response.ok || !contentType.includes('image')) throw new Error(`Snapshot fetch failed: ${response.status}`)

        const blob = await response.blob()
        if (blob.size === 0) throw new Error('Snapshot blob is empty')
        nextObjectUrl = URL.createObjectURL(blob)
        setObjectUrl(nextObjectUrl)
      } catch {
        if (!controller.signal.aborted) {
          setFailed(true)
          unavailableRef.current?.()
        }
      }
    }

    void loadImage()

    return () => {
      controller.abort()
      if (nextObjectUrl) URL.revokeObjectURL(nextObjectUrl)
    }
  }, [src])

  if (!src || failed || !objectUrl) return <>{placeholder}</>

  return <img {...imageProps} src={objectUrl} onError={() => {
    setFailed(true)
    unavailableRef.current?.()
  }} />
}
