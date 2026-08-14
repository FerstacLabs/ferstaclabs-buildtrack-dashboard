import { create } from 'zustand'
import { persist } from 'zustand/middleware'
import type { AiAssistantMessage } from '../../types/projectProgress'

interface AiAssistantState {
  tenantId?: string
  userId?: string
  messages: AiAssistantMessage[]
  messagesByScope: Record<string, AiAssistantMessage[]>
  setScope: (tenantId?: string, userId?: string) => void
  addMessage: (message: Omit<AiAssistantMessage, 'id' | 'createdAt'>) => void
  clearMessages: () => void
  migrateLegacyProjectProgressMessages: () => void
}

const createId = () => `ai-msg-${Date.now()}-${Math.random().toString(16).slice(2, 8)}`

const scopeKey = (tenantId?: string, userId?: string) =>
  `${tenantId?.trim() || 'anonymous'}:${userId?.trim() || 'anonymous'}`

const readLegacyAssistantMessages = (): AiAssistantMessage[] => {
  if (typeof window === 'undefined') return []

  try {
    const legacyWorkspaceKey = ['buildtrack', 'project', 'progress'].join('-')
    const raw = window.localStorage.getItem(legacyWorkspaceKey)
    if (!raw) return []

    const parsed = JSON.parse(raw) as {
      state?: { assistantMessages?: AiAssistantMessage[] }
      assistantMessages?: AiAssistantMessage[]
    }
    const messages = parsed.state?.assistantMessages ?? parsed.assistantMessages ?? []
    return Array.isArray(messages)
      ? messages.filter((message) => message?.role && typeof message.content === 'string')
      : []
  } catch {
    return []
  }
}

export const useAiAssistantStore = create<AiAssistantState>()(
  persist(
    (set, get) => ({
      tenantId: undefined,
      userId: undefined,
      messages: [],
      messagesByScope: {},
      setScope: (tenantId, userId) => {
        const nextKey = scopeKey(tenantId, userId)
        set((state) => ({
          tenantId,
          userId,
          messages: state.messagesByScope[nextKey] ?? [],
        }))
      },
      addMessage: (message) => set((state) => {
        const key = scopeKey(state.tenantId, state.userId)
        const nextMessages = [
          ...state.messages,
          { ...message, id: createId(), createdAt: new Date().toISOString() },
        ]

        return {
          messages: nextMessages,
          messagesByScope: {
            ...state.messagesByScope,
            [key]: nextMessages,
          },
        }
      }),
      clearMessages: () => set((state) => {
        const key = scopeKey(state.tenantId, state.userId)
        return {
          messages: [],
          messagesByScope: {
            ...state.messagesByScope,
            [key]: [],
          },
        }
      }),
      migrateLegacyProjectProgressMessages: () => {
        if (typeof window === 'undefined') return

        const state = get()
        const key = scopeKey(state.tenantId, state.userId)
        const migrationKey = `buildtrack-ai-assistant:migrated:${key}`
        if (window.localStorage.getItem(migrationKey)) return
        window.localStorage.setItem(migrationKey, '1')

        if (state.messages.length > 0) return
        const legacyMessages = readLegacyAssistantMessages()
        if (!legacyMessages.length) return

        set((current) => ({
          messages: legacyMessages,
          messagesByScope: {
            ...current.messagesByScope,
            [key]: legacyMessages,
          },
        }))
      },
    }),
    {
      name: 'buildtrack-ai-assistant',
      partialize: (state) => ({
        tenantId: state.tenantId,
        userId: state.userId,
        messagesByScope: state.messagesByScope,
      }),
      version: 1,
    },
  ),
)
