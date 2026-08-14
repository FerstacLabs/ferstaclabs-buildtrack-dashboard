import { AudioOutlined, DeleteOutlined, SendOutlined, SoundOutlined } from '@ant-design/icons'
import { Button, Drawer, Input, Select, Tag, Tooltip } from 'antd'
import { useEffect, useMemo, useRef, useState } from 'react'
import { tryApiRequest } from '../../shared/api/client'
import { ALL_PROJECTS_ID, useProjectSelectionStore } from '../../stores/projectSelectionStore'
import { useAuthStore } from '../auth/authStore'
import { useProjectProgressStore } from '../projectProgress/projectProgressStore'
import { useAiAssistantStore } from './aiAssistantStore'
import { fetchTtsAudio } from './api/ttsApi'

interface AssistantApiResponse {
  answer?: string
  source?: 'openai' | 'server-fallback' | 'local-fallback' | string
  model?: string
  error?: string | null
  sourceModules?: string[]
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
  if (import.meta.env.DEV) console.log(`[AI TTS] ${message}`, details ?? '')
}

const sourceLabel = (source?: string) => {
  if (source === 'openai') return 'OpenAI cavabı'
  if (source === 'server-fallback') return 'Server xülasəsi'
  return 'Yerli xülasə'
}

const AI_ALL_PROJECTS_VALUE = '__ai_all_projects__'

export const AiAssistant = () => {
  const tenantId = useAuthStore((state) => state.tenant?.id)
  const userId = useAuthStore((state) => state.user?.id)
  const globalSelectedProjectId = useProjectSelectionStore((state) => state.selectedProjectId)
  const objects = useProjectProgressStore((state) => state.objects)
  const messages = useAiAssistantStore((state) => state.messages)
  const addAssistantMessage = useAiAssistantStore((state) => state.addMessage)
  const clearAssistantMessages = useAiAssistantStore((state) => state.clearMessages)
  const setAssistantScope = useAiAssistantStore((state) => state.setScope)
  const migrateLegacyProjectProgressMessages = useAiAssistantStore((state) => state.migrateLegacyProjectProgressMessages)
  const [open, setOpen] = useState(false)
  const [aiSelectedSiteId, setAiSelectedSiteId] = useState<string | null>(null)
  const [input, setInput] = useState('')
  const [loading, setLoading] = useState(false)
  const [speechStatus, setSpeechStatus] = useState<string | null>(null)
  const [isPreparingSpeech, setIsPreparingSpeech] = useState(false)
  const [isSpeaking, setIsSpeaking] = useState(false)
  const [activeSpeechMessageId, setActiveSpeechMessageId] = useState<string | null>(null)
  const audioRef = useRef<HTMLAudioElement | null>(null)
  const audioUrlRef = useRef<string | null>(null)
  const preparedSpeechTextRef = useRef<string | null>(null)
  const speechRecognition = getSpeechRecognition()
  const projectOptions = useMemo(
    () => objects.map((object) => ({ value: object.id, label: object.name })),
    [objects],
  )

  const cleanupAudioUrl = () => {
    if (audioRef.current) {
      audioRef.current.pause()
      audioRef.current.currentTime = 0
      audioRef.current.src = ''
      audioRef.current = null
    }

    if (audioUrlRef.current) {
      URL.revokeObjectURL(audioUrlRef.current)
      audioUrlRef.current = null
    }

    preparedSpeechTextRef.current = null
    setIsSpeaking(false)
    setIsPreparingSpeech(false)
    setActiveSpeechMessageId(null)
  }

  const stopSpeech = () => {
    if (audioRef.current) {
      audioRef.current.pause()
      audioRef.current.currentTime = 0
    }

    setIsSpeaking(false)
    setIsPreparingSpeech(false)
  }

  useEffect(() => cleanupAudioUrl, [])

  useEffect(() => {
    setAssistantScope(tenantId, userId)
    migrateLegacyProjectProgressMessages()
    setAiSelectedSiteId(null)
  }, [migrateLegacyProjectProgressMessages, setAssistantScope, tenantId, userId])

  useEffect(() => {
    if (!aiSelectedSiteId) return
    if (projectOptions.some((option) => option.value === aiSelectedSiteId)) return
    setAiSelectedSiteId(null)
  }, [aiSelectedSiteId, projectOptions])

  const openDrawer = () => {
    setAiSelectedSiteId(globalSelectedProjectId === ALL_PROJECTS_ID ? null : globalSelectedProjectId)
    setOpen(true)
  }

  const closeDrawer = () => {
    cleanupAudioUrl()
    setOpen(false)
  }

  const submitQuestion = async (question: string) => {
    const trimmed = question.trim()
    if (!trimmed) return

    const history = useAiAssistantStore.getState().messages
      .slice(-10)
      .map((item) => ({ role: item.role, content: item.content }))

    cleanupAudioUrl()
    setInput('')
    setLoading(true)
    setSpeechStatus(null)
    addAssistantMessage({ role: 'user', content: trimmed })

    const apiAnswer = await tryApiRequest<AssistantApiResponse>('/api/ai/project-assistant/chat', {
      method: 'POST',
      body: JSON.stringify({
        message: trimmed,
        selectedSiteId: aiSelectedSiteId ?? null,
        history,
      }),
    })

    cleanupAudioUrl()
    if (apiAnswer?.answer && !containsCyrillic(apiAnswer.answer)) {
      addAssistantMessage({
        role: 'assistant',
        content: apiAnswer.answer,
        source: apiAnswer.source === 'openai' ? 'openai' : 'server-fallback',
      })
    } else {
      addAssistantMessage({
        role: 'assistant',
        content: apiAnswer?.error
          ? `AI cavabı hazırda alınmadı: ${apiAnswer.error}`
          : 'AI cavabı hazırda alınmadı. Bir az sonra yenidən yoxlayın.',
        source: 'server-fallback',
      })
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

  const prepareOpenAiSpeech = async (messageId: string, textToSpeak: string): Promise<HTMLAudioElement> => {
    if (audioRef.current && audioUrlRef.current && preparedSpeechTextRef.current === textToSpeak) {
      setActiveSpeechMessageId(messageId)
      return audioRef.current
    }

    cleanupAudioUrl()
    setActiveSpeechMessageId(messageId)
    setIsPreparingSpeech(true)
    setSpeechStatus('Səs hazırlanır...')

    const blob = await fetchTtsAudio(textToSpeak)
    const audioUrl = URL.createObjectURL(blob)
    const audio = new Audio()
    audio.preload = 'auto'
    audio.src = audioUrl

    audio.onplay = () => {
      setIsSpeaking(true)
      setIsPreparingSpeech(false)
      setSpeechStatus(null)
      devLog('play success')
    }

    audio.onended = () => {
      setIsSpeaking(false)
      setIsPreparingSpeech(false)
      setSpeechStatus(null)
      if (audioRef.current) audioRef.current.currentTime = 0
      devLog('OpenAI TTS playback ended')
    }

    audio.onerror = (event) => {
      console.error('[AI TTS] audio element error', event, audio.error)
      cleanupAudioUrl()
      setActiveSpeechMessageId(messageId)
      setSpeechStatus('OpenAI səsi oxunmadı. Yenidən cəhd edin.')
    }

    audioRef.current = audio
    audioUrlRef.current = audioUrl
    preparedSpeechTextRef.current = textToSpeak
    setIsPreparingSpeech(false)
    setSpeechStatus(null)

    return audio
  }

  const toggleSpeak = async (messageId: string, text: string) => {
    const textToSpeak = text.replace(/\s+/g, ' ').trim().slice(0, 3900)
    if (!textToSpeak) return

    if (isSpeaking && activeSpeechMessageId === messageId) {
      stopSpeech()
      return
    }

    if (isPreparingSpeech && activeSpeechMessageId === messageId) {
      stopSpeech()
      return
    }

    try {
      const audio = await prepareOpenAiSpeech(messageId, textToSpeak)

      try {
        audio.currentTime = 0
        await audio.play()
      } catch (playError) {
        console.error('[AI TTS] audio.play failed', playError)
        setIsSpeaking(false)
        setIsPreparingSpeech(false)
        setActiveSpeechMessageId(messageId)
        setSpeechStatus('Səs hazırdır - başlatmaq üçün yenidən basın')
      }
    } catch (error) {
      console.error('[AI TTS] prepare failed', error)
      cleanupAudioUrl()
      setActiveSpeechMessageId(messageId)
      setSpeechStatus('OpenAI səsi oxunmadı. Yenidən cəhd edin.')
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
          onClick={openDrawer}
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
      >
        <div className="assistant-panel">
          <div className="assistant-project-toolbar">
            <span className="assistant-project-label">Layihə</span>
            <Select
              value={aiSelectedSiteId ?? AI_ALL_PROJECTS_VALUE}
              options={[
                { value: AI_ALL_PROJECTS_VALUE, label: 'Bütün layihələr' },
                ...projectOptions,
              ]}
              onChange={(value) => setAiSelectedSiteId(value === AI_ALL_PROJECTS_VALUE ? null : value)}
            />
          </div>
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
                    <span className="assistant-source-pill">{sourceLabel(item.source)}</span>
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
                      loading={isPreparingSpeech && activeSpeechMessageId === item.id}
                      onClick={() => void toggleSpeak(item.id, item.content)}
                    >
                      {activeSpeechMessageId === item.id && isPreparingSpeech
                        ? 'Səs hazırlanır...'
                        : activeSpeechMessageId === item.id && isSpeaking
                          ? 'Dayandır'
                          : activeSpeechMessageId === item.id && speechStatus === 'Səs hazırdır - başlatmaq üçün yenidən basın'
                            ? 'Səsi başlat'
                            : 'Səsli oxu'}
                    </Button>
                    {activeSpeechMessageId === item.id && speechStatus ? <span>{speechStatus}</span> : null}
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
          <Button icon={<DeleteOutlined />} onClick={() => { cleanupAudioUrl(); clearAssistantMessages() }}>Söhbəti təmizlə</Button>
        </div>
      </Drawer>
    </>
  )
}
