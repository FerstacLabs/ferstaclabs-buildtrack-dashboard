import { readdirSync, readFileSync, statSync } from 'node:fs'
import { extname, join, relative } from 'node:path'

const root = process.cwd()
const scannedRoots = ['src', 'backend/src']
const extensions = new Set(['.ts', '.tsx', '.cs'])
const skipParts = new Set(['node_modules', 'dist', 'bin', 'obj'])

const blockedVisibleTerms = [
  'Bütün obyektlər',
  'Obyekt seç',
  'Yeni obyekt',
  'obyekt məlumat',
  'cari obyekt',
  'Obyekt:',
  'Obyekt üzrə',
  'Obyektlər üzrə',
  'All objects',
  'Select object',
  'New object',
  'Все объекты',
  'Объект',
  'объект',
  'LayihÉ',
  'layihÉ',
]

const findings = []

const walk = (dir) => {
  const files = []
  for (const entry of readdirSync(dir)) {
    const fullPath = join(dir, entry)
    const relParts = relative(root, fullPath).split(/[\\/]/)
    if (relParts.some((part) => skipParts.has(part))) continue
    const stat = statSync(fullPath)
    if (stat.isDirectory()) files.push(...walk(fullPath))
    else if (extensions.has(extname(fullPath))) files.push(fullPath)
  }
  return files
}

for (const scannedRoot of scannedRoots) {
  const absoluteRoot = join(root, scannedRoot)
  for (const file of walk(absoluteRoot)) {
    const lines = readFileSync(file, 'utf8').split(/\r?\n/)
    lines.forEach((line, index) => {
      for (const term of blockedVisibleTerms) {
        if (!line.includes(term)) continue
        findings.push(`${relative(root, file)}:${index + 1}: forbidden visible terminology "${term}"`)
      }
    })
  }
}

const read = (path) => readFileSync(join(root, path), 'utf8')
const assertContains = (path, needle, message) => {
  if (!read(path).includes(needle)) findings.push(`${path}: ${message}`)
}
const assertNotContains = (path, needle, message) => {
  if (read(path).includes(needle)) findings.push(`${path}: ${message}`)
}

assertContains('src/components/ProjectSelect.tsx', "t('project.allProjects')", 'ProjectSelect must display project.allProjects')
assertContains('src/components/ProjectSelect.tsx', 'value={selectedValue}', 'ProjectSelect must be controlled with value={selectedValue}')
assertNotContains('src/components/ProjectSelect.tsx', 'defaultValue=', 'ProjectSelect must not use defaultValue')

assertContains('src/features/aiAssistant/AiAssistant.tsx', '<span>Layihə</span>', 'AI drawer must have one Layihə context label')
assertContains('src/features/aiAssistant/AiAssistant.tsx', 'selectedSiteId: selectedSiteId ?? null', 'AI request must send selectedSiteId')
assertContains('src/features/aiAssistant/AiAssistant.tsx', "label: 'Bütün layihələr'", 'AI dropdown must include Bütün layihələr option')
assertNotContains('src/features/aiAssistant/AiAssistant.tsx', 'selectedProjectId:', 'AI frontend must not send selectedProjectId')
assertNotContains('src/features/aiAssistant/AiAssistant.tsx', 'AI_ALL_SITES_VALUE', 'AI drawer must not keep the old second site dropdown sentinel')
assertNotContains('src/features/aiAssistant/AiAssistant.tsx', '<span>Obyekt</span>', 'AI drawer must not expose an Obyekt dropdown')

assertNotContains('src/features/aiAssistant/aiAssistantStore.ts', 'contextTouched', 'AI context must not persist sticky contextTouched')
assertNotContains('src/features/aiAssistant/aiAssistantStore.ts', 'selectedProjectId', 'AI store must not keep a separate selectedProjectId')
assertContains('backend/src/BuildTrack.Api/Services/OpenAiProjectAssistantService.cs', 'never call them "obyekt"', 'AI system prompt must forbid Azerbaijani obyekt terminology')
assertNotContains('backend/src/BuildTrack.Api/Services/OpenAiProjectAssistantService.cs', 'never call them "layihə"', 'AI system prompt must not forbid layihə')

if (findings.length > 0) {
  console.error('Terminology audit failed:')
  for (const finding of findings) console.error(`- ${finding}`)
  process.exit(1)
}

console.log('Terminology audit passed: user-facing project/site terminology is Layihə, AI context has one controlled Layihə selector.')
