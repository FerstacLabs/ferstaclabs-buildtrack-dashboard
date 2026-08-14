import { readFileSync } from 'node:fs'
import { join } from 'node:path'

const root = process.cwd()
const read = (path) => readFileSync(join(root, path), 'utf8')
const findings = []

const assertContains = (path, needle, message) => {
  if (!read(path).includes(needle)) findings.push(`${path}: ${message}`)
}

const assertNotContains = (path, needle, message) => {
  if (read(path).includes(needle)) findings.push(`${path}: ${message}`)
}

const autoTranslate = 'src/i18n/useAutoTranslateUi.ts'
const aiAssistant = 'src/features/aiAssistant/AiAssistant.tsx'
const projectSelect = 'src/components/ProjectSelect.tsx'

assertNotContains(autoTranslate, 'WeakMap<Text, string>', 'Auto translator must not keep the first text node value forever.')
assertContains(autoTranslate, 'lastSource', 'Auto translator must track the latest React source text.')
assertContains(autoTranslate, 'lastTranslated', 'Auto translator must distinguish its own translated DOM writes.')
assertContains(autoTranslate, "currentValue === previous.lastTranslated", 'Translated DOM writes must preserve the same source text.')
assertContains(autoTranslate, ': currentValue', 'React-updated text must become the new source text.')

;[
  '.ant-select',
  '.ant-select-selector',
  '.ant-select-selection-item',
  '.ant-select-dropdown',
  '.ant-table',
  '.ant-input',
  '.ant-picker',
  '.ant-modal',
  '.ant-drawer',
  '.ant-dropdown',
].forEach((selector) => {
  assertContains(autoTranslate, selector, `Auto translator must skip dynamic Ant Design selector ${selector}.`)
})

assertContains(aiAssistant, 'const currentGlobalProjectId = useProjectSelectionStore.getState().selectedProjectId', 'AI drawer must read global project selection at open/click time.')
assertContains(aiAssistant, 'value={aiSelectedSiteId ?? AI_ALL_PROJECTS_VALUE}', 'AI Select must be controlled by local selectedSiteId.')
assertContains(aiAssistant, 'selectedSiteId: aiSelectedSiteId ?? null', 'AI request must use the same local selectedSiteId.')
assertNotContains(aiAssistant, 'projectOptions.some((option) => option.value === aiSelectedSiteId)', 'AI selection must not reset to all projects while options are temporarily empty.')
assertNotContains(aiAssistant, 'Kontekst:', 'AI drawer must not render a duplicate context text block.')
assertNotContains(aiAssistant, 'extra={<Button', 'AI drawer must not render a second close button.')

assertContains(projectSelect, 'value={selectedValue}', 'Main ProjectSelect must stay controlled.')
assertNotContains(projectSelect, 'defaultValue=', 'Main ProjectSelect must not use defaultValue.')

if (findings.length) {
  console.error('Auto-translate audit failed:')
  findings.forEach((finding) => console.error(`- ${finding}`))
  process.exit(1)
}

console.log('Auto-translate audit passed: dynamic React/Ant text is protected from stale DOM translation.')
