import { AudioOutlined, CloseOutlined, DeleteOutlined, SendOutlined, SoundOutlined } from '@ant-design/icons'
import { Button, Drawer, Input, Tag, Tooltip } from 'antd'
import { useEffect, useMemo, useRef, useState } from 'react'
import { useLocation } from 'react-router-dom'
import { tryApiRequest } from '../../shared/api/client'
import { ALL_OBJECTS_ID } from '../projectProgress/projectSelectors'
import { useProjectProgressStore } from '../projectProgress/projectProgressStore'
import { getAssistantAnswer } from './aiAssistantEngine'
import { fetchTtsAudio } from './api/ttsApi'
import { buildAiProjectContext, type AiProjectContext } from './aiContextBuilder'

interface AssistantApiResponse {
  answer?: string
  source?: 'openai' | 'local-fallback' | string
  model?: string
  error?: string | null
}

interface SpeechRecognitionLike {
  lang: string
  interimResults: boolean
  maxAlternatives: number
  start: () => void
  onresult: ((event: { results: ArrayLike<{ 0: { transcript: string } }> }) => void) | null
  onerror: (() => void) | null
}

type SpeechRecognitionConstructor = new () => SpeechRecognitionLike

const quickPrompts = [
  'Bugünkü ümumi vəziyyət necədir?',
  'Ən kritik risklər hansılardır?',
  'Hansı işlər gecikir?',
  'Büdcə vəziyyəti necədir?',
  'İşçi heyətinin vəziyyəti necədir?',
  'Bu gün nəyə diqqət etməliyəm?',
  'Material çatışmazlığı varmı?',
  'Prorab son nə qeyd edib?',
  'Maaş xərci nə qədərdir?',
  'Vacib məlumatları özün təqdim et',
]

const pageObjectFilterKeyByPath: Array<[string, string]> = [
  ['/estimate', 'estimate'],
  ['/project-progress/estimate', 'estimate'],
  ['/crews', 'crews'],
  ['/project-progress/crews', 'crews'],
  ['/workers', 'workers'],
  ['/timeline', 'timeline'],
  ['/project-progress/timeline', 'timeline'],
  ['/daily-reports', 'dailyReports'],
  ['/materials', 'materials'],
  ['/daily-attendance', 'attendance'],
  ['/site-hours', 'siteHours'],
  ['/risk-workers', 'riskWorkers'],
  ['/delays-permissions', 'delays'],
  ['/payroll', 'payroll'],
  ['/supervisor-audit', 'audit'],
  ['/export', 'export'],
  ['/', 'dashboard'],
]

const getSpeechRecognition = () => {
  const browserWindow = window as unknown as {
    SpeechRecognition?: SpeechRecognitionConstructor
    webkitSpeechRecognition?: SpeechRecognitionConstructor
  }
  return browserWindow.SpeechRecognition ?? browserWindow.webkitSpeechRecognition
}

const ConstructionBotIcon = () => (
  <svg width="25" height="25" viewBox="0 0 32 32" role="img" aria-hidden="true" focusable="false">
    <path d="M8 14.2c0-4.1 3.3-7.4 7.4-7.4h1.2c4.1 0 7.4 3.3 7.4 7.4v5.2c0 3.1-2.5 5.6-5.6 5.6h-4.8C10.5 25 8 22.5 8 19.4z" fill="#f7fbff" />
    <path d="M9.2 13.4h13.6c-.5-3.1-3.1-5.4-6.2-5.4h-1.2c-3.1 0-5.7 2.3-6.2 5.4z" fill="#ffb703" />
    <path d="M13.8 5.5h4.4l.9 3.7h-6.2z" fill="#ffd166" />
    <path d="M7.2 14.2h17.6" stroke="#0d5f50" strokeWidth="2.1" strokeLinecap="round" />
    <path d="M12.2 17.2h.1M19.7 17.2h.1" stroke="#0b2f45" strokeWidth="3.4" strokeLinecap="round" />
    <path d="M13.2 21c1.8 1.1 3.8 1.1 5.6 0" stroke="#0d5f50" strokeWidth="1.8" strokeLinecap="round" />
    <path d="M5.8 17.1h2.1v4H5.8a2 2 0 0 1-2-2 2 2 0 0 1 2-2ZM24.1 17.1h2.1a2 2 0 0 1 2 2 2 2 0 0 1-2 2h-2.1z" fill="#10b7a6" />
  </svg>
)

const containsCyrillic = (value: string) => /[\u0400-\u04FF]/.test(value)
const devLog = (message: string, details?: unknown) => {
  if (import.meta.env.DEV) console.debug(`[BuildTrack AI] ${message}`, details ?? '')
}

const buildAssistantPayloadContext = (context: AiProjectContext) => ({
  selectedObjectId: context.selectedObject?.id ?? null,
  selectedObjectName: context.selectedObject?.name ?? 'Bütün obyektlər',
  summary: context.summary,
  objects: context.objects,
  stages: context.stages.slice(0, 18),
  workItems: context.workItems.slice(0, 30),
  crews: context.crews.slice(0, 16),
  workers: context.workers.slice(0, 30),
  attendance: context.attendance.slice(0, 30),
  payroll: context.payroll.slice(0, 20),
  materials: context.materials.slice(0, 25),
  dailyReports: context.dailyReports.slice(0, 12),
  risks: context.risks.slice(0, 20),
  delays: context.delays.slice(0, 20),
  audit: context.audit.slice(0, 16),
  exportRows: context.exportRows.slice(0, 16),
  topInsights: context.topInsights.slice(0, 10),
})

const getBrowserVoices = async () => {
  if (!('speechSynthesis' in window)) return []
  const current = window.speechSynthesis.getVoices()
  if (current.length) return current

  return await new Promise<SpeechSynthesisVoice[]>((resolve) => {
    const finish = () => resolve(window.speechSynthesis.getVoices())
    window.speechSynthesis.onvoiceschanged = finish
    window.setTimeout(finish, 500)
  })
}

const pickBrowserVoice = (voices: SpeechSynthesisVoice[]) =>
  voices.find((voice) => voice.lang.toLowerCase() === 'az-az')
    ?? voices.find((voice) => voice.lang.toLowerCase().startsWith('az'))
    ?? voices.find((voice) => voice.lang.toLowerCase() === 'tr-tr')
    ?? voices.find((voice) => voice.lang.toLowerCase().startsWith('tr'))
    ?? voices.find((voice) => voice.lang.toLowerCase() === 'en-us')
    ?? voices.find((voice) => !voice.lang.toLowerCase().startsWith('ru'))
    ?? null

export const AiAssistant = () => {
  const data = useProjectProgressStore()
  const addAssistantMessage = useProjectProgressStore((state) => state.addAssistantMessage)
  const clearAssistantMessages = useProjectProgressStore((state) => state.clearAssistantMessages)
  const location = useLocation()
  const [open, setOpen] = useState(false)
  const [input, setInput] = useState('')
  const [loading, setLoading] = useState(false)
  const [voiceNoteByMessageId, setVoiceNoteByMessageId] = useState<Record<string, string>>({})
  const [speakingMessageId, setSpeakingMessageId] = useState<string | null>(null)
  const [preparingMessageId, setPreparingMessageId] = useState<string | null>(null)
  const audioRef = useRef<HTMLAudioElement | null>(null)
  const audioUrlRef = useRef<string | null>(null)
  const speechRecognition = getSpeechRecognition()
  const messages = data.assistantMessages
  const pageFilterKey = pageObjectFilterKeyByPath.find(([path]) => (path === '/' ? location.pathname === '/' : location.pathname.startsWith(path)))?.[1] ?? 'dashboard'
  const selectedObjectId = data.selectedObjectIdByPage[pageFilterKey] ?? ALL_OBJECTS_ID
  const context = useMemo(() => buildAiProjectContext({ data, objectId: selectedObjectId }), [data, selectedObjectId])
  const contextLabel = context.selectedObject?.name ?? 'Bütün obyektlər'

  const stopSpeaking = () => {
    if (audioRef.current) {
      audioRef.current.pause()
      audioRef.current.src = ''
      audioRef.current = null
    }
    if (audioUrlRef.current) {
      URL.revokeObjectURL(audioUrlRef.current)
      audioUrlRef.current = null
    }
    if ('speechSynthesis' in window) window.speechSynthesis.cancel()
    setSpeakingMessageId(null)
    setPreparingMessageId(null)
  }

  useEffect(() => stopSpeaking, [])

  const closeDrawer = () => {
    stopSpeaking()
    setOpen(false)
  }

  const addLocalAnswer = (question: string) => {
    const localAnswer = getAssistantAnswer(question, buildAiProjectContext({ data, objectId: selectedObjectId }))
    addAssistantMessage({ role: 'assistant', content: localAnswer.answer, source: 'local-fallback' })
  }

  const submitQuestion = async (question: string) => {
    const trimmed = question.trim()
    if (!trimmed) return

    stopSpeaking()
    setInput('')
    setLoading(true)
    setVoiceNoteByMessageId({})
    addAssistantMessage({ role: 'user', content: trimmed })

    const history = messages
      .slice(-8)
      .map((item) => ({ role: item.role, content: item.content }))

    const apiAnswer = await tryApiRequest<AssistantApiResponse>('/api/ai/project-assistant/chat', {
      method: 'POST',
      body: JSON.stringify({
        message: trimmed,
        context: buildAssistantPayloadContext(context),
        history,
      }),
    })

    stopSpeaking()
    if (apiAnswer?.source === 'openai' && apiAnswer.answer && !containsCyrillic(apiAnswer.answer)) {
      addAssistantMessage({ role: 'assistant', content: apiAnswer.answer, source: 'openai' })
    } else {
      addLocalAnswer(trimmed)
    }

    setLoading(false)
  }

  const startVoiceInput = () => {
    if (!speechRecognition) return
    const recognition = new speechRecognition()
    recognition.lang = 'az-AZ'
    recognition.interimResults = false
    recognition.maxAlternatives = 1
    recognition.onresult = (event) => setInput(event.results[0][0].transcript)
    recognition.onerror = () => undefined
    recognition.start()
  }

  const speakWithBrowserFallback = async (messageId: string, text: string) => {
    if (!('speechSynthesis' in window)) {
      setVoiceNoteByMessageId((state) => ({ ...state, [messageId]: 'Səsli oxuma bu brauzerdə dəstəklənmir.' }))
      return
    }

    devLog('TTS error fallback used')
    window.speechSynthesis.cancel()
    const voices = await getBrowserVoices()
    const voice = pickBrowserVoice(voices)
    const utterance = new SpeechSynthesisUtterance(text)
    utterance.lang = voice?.lang ?? 'az-AZ'
    utterance.voice = voice
    utterance.rate = 0.95
    utterance.pitch = 1
    utterance.volume = 1
    utterance.onend = () => {
      setSpeakingMessageId(null)
      devLog('Browser speech playback ended')
    }
    utterance.onerror = () => setSpeakingMessageId(null)
    setSpeakingMessageId(messageId)
    setPreparingMessageId(null)
    window.speechSynthesis.speak(utterance)
  }

  const toggleSpeak = async (messageId: string, text: string) => {
    if (!text.trim()) return
    if (speakingMessageId === messageId || preparingMessageId === messageId) {
      stopSpeaking()
      return
    }

    stopSpeaking()
    setVoiceNoteByMessageId((state) => {
      const next = { ...state }
      delete next[messageId]
      return next
    })
    setPreparingMessageId(messageId)

    try {
      const blob = await fetchTtsAudio(text)
      const url = URL.createObjectURL(blob)
      const audio = new Audio(url)
      audioRef.current = audio
      audioUrlRef.current = url
      audio.onplay = () => {
        setPreparingMessageId(null)
        setSpeakingMessageId(messageId)
        devLog('OpenAI TTS playback started')
      }
      audio.onended = () => {
        devLog('OpenAI TTS playback ended')
        stopSpeaking()
      }
      audio.onerror = () => {
        stopSpeaking()
        setVoiceNoteByMessageId((state) => ({ ...state, [messageId]: 'OpenAI səsi alınmadı, browser səsi ilə yoxlanılır.' }))
        void speakWithBrowserFallback(messageId, text)
      }
      await audio.play()
    } catch (error) {
      devLog('Browser fallback used', error)
      stopSpeaking()
      setVoiceNoteByMessageId((state) => ({ ...state, [messageId]: 'OpenAI səsi alınmadı, browser səsi ilə yoxlanılır.' }))
      await speakWithBrowserFallback(messageId, text)
    }
  }

  return (
    <>
      <Tooltip title="AI köməkçi">
        <Button
          className="ai-assistant-button"
          type="primary"
          shape="circle"
          size="large"
          icon={<ConstructionBotIcon />}
          onClick={() => setOpen(true)}
          aria-label="AI köməkçi"
          title="AI köməkçi"
        />
      </Tooltip>
      <Drawer
        title={(
          <div className="assistant-title">
            <strong>AI Rəhbər Köməkçisi</strong>
            <span>Layihə, smeta, briqada, risk və maliyyə üzrə suallar verin.</span>
          </div>
        )}
        open={open}
        width={480}
        onClose={closeDrawer}
        extra={<Button icon={<CloseOutlined />} onClick={closeDrawer} />}
      >
        <div className="assistant-panel">
          <div className="assistant-context-line">Kontekst: <strong>{contextLabel}</strong></div>
          <div className="assistant-prompts">
            {quickPrompts.map((prompt) => (
              <button type="button" key={prompt} onClick={() => void submitQuestion(prompt)}>
                {prompt}
              </button>
            ))}
          </div>

          <div className="assistant-messages">
            {messages.length ? messages.map((item) => (
              <div className={`assistant-message ${item.role}`} key={item.id}>
                <div className="assistant-message-header">
                  <Tag color={item.role === 'assistant' ? 'green' : 'blue'}>{item.role === 'assistant' ? 'Rəhbər köməkçisi' : 'Sual'}</Tag>
                  {item.role === 'assistant' ? (
                    <span className="assistant-source-pill">{item.source === 'openai' ? 'OpenAI cavabı' : 'Lokal analiz'}</span>
                  ) : null}
                </div>
                <p>{item.content}</p>
                {item.role === 'assistant' ? (
                  <div className="assistant-speech-block">
                    <Button
                      block
                      size="small"
                      icon={<SoundOutlined />}
                      disabled={!item.content.trim()}
                      loading={preparingMessageId === item.id}
                      onClick={() => void toggleSpeak(item.id, item.content)}
                    >
                      {preparingMessageId === item.id ? 'Səs hazırlanır...' : speakingMessageId === item.id ? 'Dayandır' : 'Səsli oxu'}
                    </Button>
                    {voiceNoteByMessageId[item.id] ? <span>{voiceNoteByMessageId[item.id]}</span> : null}
                  </div>
                ) : null}
              </div>
            )) : <div className="empty-soft">Rəhbər brifinqi üçün sual yazın və ya hazır ssenarilərdən birini seçin.</div>}
          </div>

          <div className="aiComposer">
            {speechRecognition ? (
              <Tooltip title="Səslə soruş">
                <Button className="aiComposerButton" icon={<AudioOutlined />} onClick={startVoiceInput} />
              </Tooltip>
            ) : <span />}
            <Input.TextArea
              className="aiComposerInput"
              value={input}
              autoSize={{ minRows: 1, maxRows: 3 }}
              onChange={(event) => setInput(event.target.value)}
              onPressEnter={(event) => {
                if (!event.shiftKey) {
                  event.preventDefault()
                  void submitQuestion(input)
                }
              }}
              placeholder="Sualınızı yazın və ya səsli deyin..."
            />
            <Button className="aiComposerSendButton" type="primary" loading={loading} icon={<SendOutlined />} onClick={() => void submitQuestion(input)} />
          </div>
          <Button icon={<DeleteOutlined />} onClick={() => { stopSpeaking(); clearAssistantMessages() }}>Söhbəti təmizlə</Button>
        </div>
      </Drawer>
    </>
  )
}
