import fs from 'node:fs'
import path from 'node:path'

const root = process.cwd()
const scanRoots = ['src']
const allowedFiles = new Set([
  path.normalize('src/components/layout/AppLayout.tsx'),
  path.normalize('src/components/ProjectSelect.tsx'),
  path.normalize('src/components/filters/ObjectFilter.tsx'),
  path.normalize('src/stores/projectSelectionStore.ts'),
  path.normalize('src/features/projectProgress/projectProgressStore.ts'),
])

const patterns = [
  { name: 'defaultValue', regex: /defaultValue\s*=/i },
  { name: 'localStorage.getItem.*project', regex: /localStorage\.getItem\([^)\n]*(project|object|selected)/i },
  { name: 'useState.*project', regex: /useState[^;\n]*project/i },
  { name: 'useState.*object', regex: /useState[^;\n]*object/i },
  { name: 'selectedProject', regex: /selectedProject/i },
  { name: 'setSelectedProject', regex: /setSelectedProject/i },
  { name: 'projectDropdown', regex: /projectDropdown/i },
  { name: 'Select.*defaultValue', regex: /<Select[\s\S]{0,240}defaultValue/i },
  { name: 'currentProject useMemo with empty deps', regex: /currentProject[\s\S]{0,160}useMemo\([\s\S]{0,240}\[\s*\]/i },
  { name: 'legacy selectedObjectId store read', regex: /\.(selectedObjectId|selectedObjectIdByPage)\b/ },
  { name: 'legacy selected object setter', regex: /setSelectedObject(ForPage|Id)\b/ },
  { name: 'global selected project getState read', regex: /getState\(\)\.selectedProjectId\b/ },
  { name: 'window.location.reload', regex: /window\.location\.reload/i },
]

const extensions = new Set(['.ts', '.tsx', '.js', '.jsx', '.mjs'])

const walk = (dir) => {
  const entries = fs.readdirSync(dir, { withFileTypes: true })
  return entries.flatMap((entry) => {
    const fullPath = path.join(dir, entry.name)
    if (entry.isDirectory()) {
      if (['node_modules', 'dist', 'bin', 'obj'].includes(entry.name)) return []
      return walk(fullPath)
    }

    return extensions.has(path.extname(entry.name)) ? [fullPath] : []
  })
}

const findings = []

for (const scanRoot of scanRoots) {
  for (const file of walk(path.join(root, scanRoot))) {
    const relative = path.normalize(path.relative(root, file))
    const text = fs.readFileSync(file, 'utf8')
    const lines = text.split(/\r?\n/)

    lines.forEach((line, index) => {
      patterns.forEach((pattern) => {
        if (!pattern.regex.test(line)) return
        if (allowedFiles.has(relative)) return
        if (line.includes('useProjectSelectionStore')) return
        findings.push({
          file: relative,
          line: index + 1,
          pattern: pattern.name,
          text: line.trim(),
        })
      })
    })
  }
}

console.log('Project state audit')
console.log(`Checked roots: ${scanRoots.join(', ')}`)
console.log(`Allowed global state files: ${Array.from(allowedFiles).join(', ')}`)

const projectSelectPath = path.join(root, 'src/components/ProjectSelect.tsx')
const projectSelectText = fs.readFileSync(projectSelectPath, 'utf8')
if (!/value=\{selectedValue\}/.test(projectSelectText)) {
  findings.push({
    file: path.normalize('src/components/ProjectSelect.tsx'),
    line: 1,
    pattern: 'ProjectSelect controlled value',
    text: 'ProjectSelect must use value={selectedValue}.',
  })
}

if (/defaultValue\s*=/.test(projectSelectText)) {
  findings.push({
    file: path.normalize('src/components/ProjectSelect.tsx'),
    line: 1,
    pattern: 'ProjectSelect defaultValue',
    text: 'ProjectSelect must not use defaultValue.',
  })
}

const objectFilterPath = path.join(root, 'src/components/filters/ObjectFilter.tsx')
const objectFilterText = fs.readFileSync(objectFilterPath, 'utf8')
if (/useState|<Select|useProjectProgressStore|localStorage/.test(objectFilterText)) {
  findings.push({
    file: path.normalize('src/components/filters/ObjectFilter.tsx'),
    line: 1,
    pattern: 'ObjectFilter thin wrapper',
    text: 'ObjectFilter must stay a thin wrapper around ProjectSelect without local state/store reads.',
  })
}

if (!findings.length) {
  console.log('No disallowed local project selection patterns found.')
  console.log('ProjectSelect controlled value check passed.')
  console.log('ObjectFilter thin-wrapper check passed.')
  process.exit(0)
}

console.log(`Potential local/stale project state patterns: ${findings.length}`)
findings.forEach((finding) => {
  console.log(`${finding.file}:${finding.line} [${finding.pattern}] ${finding.text}`)
})
