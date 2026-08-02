import fs from 'node:fs'
import path from 'node:path'

const root = process.cwd()
const scanRoots = ['src']
const allowedFiles = new Set([
  path.normalize('src/components/ProjectSelect.tsx'),
  path.normalize('src/components/filters/ObjectFilter.tsx'),
  path.normalize('src/stores/projectSelectionStore.ts'),
  path.normalize('src/features/projectProgress/projectProgressStore.ts'),
])

const patterns = [
  { name: 'defaultValue=.*project', regex: /defaultValue\s*=\s*{?[^}\n]*project/i },
  { name: 'localStorage.getItem.*project', regex: /localStorage\.getItem\([^)\n]*project/i },
  { name: 'useState.*project', regex: /useState[^;\n]*project/i },
  { name: 'selectedProject', regex: /selectedProject/i },
  { name: 'setSelectedProject', regex: /setSelectedProject/i },
  { name: 'projectDropdown', regex: /projectDropdown/i },
  { name: 'Select.*defaultValue', regex: /<Select[\s\S]{0,240}defaultValue/i },
  { name: 'legacy selectedObjectId store read', regex: /\.(selectedObjectId|selectedObjectIdByPage)\b/ },
  { name: 'legacy selected object setter', regex: /setSelectedObject(ForPage|Id)\b/ },
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

if (!findings.length) {
  console.log('No disallowed local project selection patterns found.')
  process.exit(0)
}

console.log(`Potential local/stale project state patterns: ${findings.length}`)
findings.forEach((finding) => {
  console.log(`${finding.file}:${finding.line} [${finding.pattern}] ${finding.text}`)
})
