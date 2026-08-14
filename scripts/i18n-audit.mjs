import { readdirSync, readFileSync, statSync } from 'node:fs'
import { join, relative } from 'node:path'

const root = process.cwd()
const srcRoot = join(root, 'src')
const extensions = new Set(['.ts', '.tsx'])
const skipParts = new Set(['node_modules', 'dist', 'i18n'])
const suspiciousWords = [
  'Yeni',
  'Yadda saxla',
  'Ləğv et',
  'İmtina',
  'İşçi',
  'Smeta',
  'Material',
  'Tanınmayan',
  'Davamiyyət',
  'Yoxdur',
  'Az əvvəl',
  'Yenilənib',
  'Ayarlar',
  'Briqada',
  'Maaş',
  'Gecikir',
  'Başlamayıb',
  'İcradadır',
  'Şəkil',
  'Layihə seçin',
]

const walk = (dir) => {
  const files = []
  for (const entry of readdirSync(dir)) {
    const fullPath = join(dir, entry)
    const rel = relative(root, fullPath)
    if ([...skipParts].some((part) => rel.split(/[\\/]/).includes(part))) continue
    const stat = statSync(fullPath)
    if (stat.isDirectory()) files.push(...walk(fullPath))
    else if (extensions.has(fullPath.slice(fullPath.lastIndexOf('.')))) files.push(fullPath)
  }
  return files
}

let count = 0
for (const file of walk(srcRoot)) {
  const lines = readFileSync(file, 'utf8').split(/\r?\n/)
  lines.forEach((line, index) => {
    if (!suspiciousWords.some((word) => line.includes(word))) return
    if (line.includes('data-i18n-skip')) return
    count += 1
    console.log(`${relative(root, file)}:${index + 1}: ${line.trim()}`)
  })
}

console.log(`\nPotential hardcoded i18n findings: ${count}`)
console.log('Note: this audit is advisory and does not fail the build. User-entered/seed data may appear here intentionally.')
