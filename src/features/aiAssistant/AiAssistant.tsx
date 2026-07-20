import { AudioOutlined, CloseOutlined, DeleteOutlined, SendOutlined, SoundOutlined } from '@ant-design/icons'
import { Button, Drawer, Input, Space, Tag, Tooltip } from 'antd'
import { useMemo, useState } from 'react'
import { useLocation } from 'react-router-dom'
import { tryApiRequest } from '../../shared/api/client'
import { ALL_OBJECTS_ID } from '../projectProgress/projectSelectors'
import { useProjectProgressStore } from '../projectProgress/projectProgressStore'
import { buildAiProjectContext } from './aiContextBuilder'
import { getAssistantAnswer } from './aiAssistantEngine'

interface AssistantApiResponse {
  answer?: string
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
  'Hazırda ən kritik risklər hansılardır?',
  'Hansı layihələr plan üzrə getmir?',
  'Büdcə vəziyyəti necədir?',
  'İşçi heyətinin vəziyyəti necədir?',
  'Bu gün ilk növbədə nəyə diqqət etməliyəm?',
  'Təhlükəsizliklə bağlı hər hansı problem varmı?',
  'Mənə vacib məlumatları özün təqdim et',
  'Monolit briqadasının vəziyyəti necədir?',
  'Hansı material azalır?',
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

export const AiAssistant = () => {
  const data = useProjectProgressStore()
  const addAssistantMessage = useProjectProgressStore((state) => state.addAssistantMessage)
  const clearAssistantMessages = useProjectProgressStore((state) => state.clearAssistantMessages)
  const location = useLocation()
  const [open, setOpen] = useState(false)
  const [input, setInput] = useState('')
  const [loading, setLoading] = useState(false)
  const speechRecognition = getSpeechRecognition()
  const canSpeak = 'speechSynthesis' in window
  const messages = data.assistantMessages
  const pageFilterKey = pageObjectFilterKeyByPath.find(([path]) => (path === '/' ? location.pathname === '/' : location.pathname.startsWith(path)))?.[1] ?? 'dashboard'
  const selectedObjectId = data.selectedObjectIdByPage[pageFilterKey] ?? ALL_OBJECTS_ID
  const context = useMemo(() => buildAiProjectContext({ data, objectId: selectedObjectId }), [data, selectedObjectId])
  const contextLabel = context.selectedObject?.name ?? 'Bütün obyektlər'

  const submitQuestion = async (question: string) => {
    const trimmed = question.trim()
    if (!trimmed) return
    setInput('')
    setLoading(true)
    addAssistantMessage({ role: 'user', content: trimmed })
    const localAnswer = getAssistantAnswer(trimmed, buildAiProjectContext({ data, objectId: selectedObjectId }))
    addAssistantMessage({ role: 'assistant', content: localAnswer.answer })
    setLoading(false)
    void tryApiRequest<AssistantApiResponse>('/api/ai/project-assistant/chat', {
      method: 'POST',
      body: JSON.stringify({
        message: trimmed,
        projectId: data.project.id,
        objectId: selectedObjectId === ALL_OBJECTS_ID ? null : selectedObjectId,
        intent: localAnswer.intent,
      }),
    })
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

  const speak = (text: string) => {
    if (!canSpeak) return
    window.speechSynthesis.cancel()
    const utterance = new SpeechSynthesisUtterance(text)
    utterance.lang = 'az-AZ'
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
            <span>Layihə, smeta, briqada, risk və maliyyə üzrə suallar verin</span>
          </div>
        )}
        open={open}
        width={480}
        onClose={() => setOpen(false)}
        extra={<Button icon={<CloseOutlined />} onClick={() => setOpen(false)} />}
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
                <Tag color={item.role === 'assistant' ? 'green' : 'blue'}>{item.role === 'assistant' ? 'Rəhbər köməkçisi' : 'Sual'}</Tag>
                <p>{item.content}</p>
                {item.role === 'assistant' && canSpeak ? (
                  <Button size="small" icon={<SoundOutlined />} onClick={() => speak(item.content)}>Səsli oxu</Button>
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
              placeholder="Rəhbər sualınızı yazın..."
            />
            <Button type="primary" loading={loading} icon={<SendOutlined />} onClick={() => void submitQuestion(input)} />
          </Space.Compact>
          <Button icon={<DeleteOutlined />} onClick={clearAssistantMessages}>Söhbəti təmizlə</Button>
        </div>
      </Drawer>
    </>
  )
}
