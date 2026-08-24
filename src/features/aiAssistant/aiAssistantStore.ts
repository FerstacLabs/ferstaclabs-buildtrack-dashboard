import { create } from 'zustand'
import { createJSONStorage, persist } from 'zustand/middleware'
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
        void get()
      },
    }),
    {
      name: 'buildtrack-ai-assistant',
      storage: createJSONStorage(() => window.sessionStorage),
      partialize: (state) => ({
        tenantId: state.tenantId,
        userId: state.userId,
        messagesByScope: state.messagesByScope,
      }),
      version: 4,
    },
  ),
)
