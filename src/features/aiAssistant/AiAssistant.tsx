import { AudioOutlined, CloseOutlined, DeleteOutlined, SendOutlined, SoundOutlined } from '@ant-design/icons'
import { Button, Drawer, Input, Space, Tag, Tooltip } from 'antd'
import { useMemo, useState } from 'react'
import { useLocation } from 'react-router-dom'
import { tryApiRequest } from '../../shared/api/client'
import { ALL_OBJECTS_ID } from '../projectProgress/projectSelectors'
import { useProjectProgressStore } from '../projectProgress/projectProgressStore'
import { getAssistantAnswer } from './aiAssistantEngine'
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

const pickVoice = () => {
  const voices = window.speechSynthesis.getVoices()
  return voices.find((voice) => voice.lang.toLowerCase().startsWith('az'))
    ?? voices.find((voice) => voice.lang.toLowerCase().startsWith('tr'))
    ?? null
}

export const AiAssistant = () => {
  const data = useProjectProgressStore()
  const addAssistantMessage = useProjectProgressStore((state) => state.addAssistantMessage)
  const clearAssistantMessages = useProjectProgressStore((state) => state.clearAssistantMessages)
  const location = useLocation()
  const [open, setOpen] = useState(false)
  const [input, setInput] = useState('')
  const [loading, setLoading] = useState(false)
  const [fallbackNote, setFallbackNote] = useState<string | null>(null)
  const [voiceNote, setVoiceNote] = useState<string | null>(null)
  const [speakingMessageId, setSpeakingMessageId] = useState<string | null>(null)
  const speechRecognition = getSpeechRecognition()
  const canSpeak = 'speechSynthesis' in window
  const messages = data.assistantMessages
  const pageFilterKey = pageObjectFilterKeyByPath.find(([path]) => (path === '/' ? location.pathname === '/' : location.pathname.startsWith(path)))?.[1] ?? 'dashboard'
  const selectedObjectId = data.selectedObjectIdByPage[pageFilterKey] ?? ALL_OBJECTS_ID
  const context = useMemo(() => buildAiProjectContext({ data, objectId: selectedObjectId }), [data, selectedObjectId])
  const contextLabel = context.selectedObject?.name ?? 'Bütün obyektlər'

  const stopSpeaking = () => {
    if (!canSpeak) return
    window.speechSynthesis.cancel()
    setSpeakingMessageId(null)
  }

  const closeDrawer = () => {
    stopSpeaking()
    setOpen(false)
  }

  const addLocalAnswer = (question: string) => {
    const localAnswer = getAssistantAnswer(question, buildAiProjectContext({ data, objectId: selectedObjectId }))
    addAssistantMessage({ role: 'assistant', content: localAnswer.answer })
    setFallbackNote('Lokal analiz istifadə olundu.')
  }

  const submitQuestion = async (question: string) => {
    const trimmed = question.trim()
    if (!trimmed) return

    setInput('')
    setLoading(true)
    setFallbackNote(null)
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

    if (apiAnswer?.source === 'openai' && apiAnswer.answer && !containsCyrillic(apiAnswer.answer)) {
      addAssistantMessage({ role: 'assistant', content: apiAnswer.answer })
      setFallbackNote(null)
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

  const toggleSpeak = (messageId: string, text: string) => {
    if (!canSpeak) return
    if (speakingMessageId === messageId) {
      stopSpeaking()
      return
    }

    const voice = pickVoice()
    if (!voice) {
      setVoiceNote('Bu brauzerdə Azərbaycan/Türk səsi tapılmadı.')
      return
    }

    window.speechSynthesis.cancel()
    const utterance = new SpeechSynthesisUtterance(text)
    utterance.lang = voice.lang
    utterance.voice = voice
    utterance.rate = 0.95
    utterance.pitch = 1
    utterance.volume = 1
    utterance.onend = () => setSpeakingMessageId(null)
    utterance.onerror = () => setSpeakingMessageId(null)
    setVoiceNote(null)
    setSpeakingMessageId(messageId)
    window.speechSynthesis.speak(utterance)
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
          {fallbackNote ? <div className="assistant-note">{fallbackNote}</div> : null}
          {voiceNote ? <div className="assistant-note warning">{voiceNote}</div> : null}
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
                <Tag color={item.role === 'assistant' ? 'green' : 'blue'}>{item.role === 'assistant' ? 'Rəhbər köməkçisi' : 'Sual'}</Tag>
                <p>{item.content}</p>
                {item.role === 'assistant' && canSpeak ? (
                  <Button size="small" icon={<SoundOutlined />} onClick={() => toggleSpeak(item.id, item.content)}>
                    {speakingMessageId === item.id ? 'Dayandır' : 'Səsli oxu'}
                  </Button>
                ) : null}
              </div>
            )) : <div className="empty-soft">Rəhbər brifinqi üçün sual yazın və ya hazır ssenarilərdən birini seçin.</div>}
          </div>

          <Space.Compact className="assistant-input">
            {speechRecognition ? (
              <Tooltip title="Səslə soruş">
                <Button icon={<AudioOutlined />} onClick={startVoiceInput} />
              </Tooltip>
            ) : null}
            <Input.TextArea
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
            <Button type="primary" loading={loading} icon={<SendOutlined />} onClick={() => void submitQuestion(input)} />
          </Space.Compact>
          <Button icon={<DeleteOutlined />} onClick={clearAssistantMessages}>Söhbəti təmizlə</Button>
        </div>
      </Drawer>
    </>
  )
}
