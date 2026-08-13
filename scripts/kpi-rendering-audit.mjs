import fs from 'node:fs'
import path from 'node:path'

const root = process.cwd()
const read = (relativePath) => fs.readFileSync(path.join(root, relativePath), 'utf8')

const findings = []
const assertIncludes = (file, text, label) => {
  if (!read(file).includes(text)) {
    findings.push(`${file}: missing ${label}`)
  }
}

const stableMetricFile = 'src/components/ui/StableMetricValue.tsx'
const kpiCardFile = 'src/components/ui/KpiCard.tsx'
const payrollFile = 'src/features/payroll/PayrollPage.tsx'
const attendanceLiveFile = 'src/features/attendanceLive/AttendanceLivePage.tsx'

assertIncludes(stableMetricFile, 'useLayoutEffect', 'layout-effect synchronization')
assertIncludes(stableMetricFile, 'requestAnimationFrame', 'post-render frame synchronization')
assertIncludes(stableMetricFile, 'MutationObserver', 'scoped mutation observer recovery')
assertIncludes(stableMetricFile, 'data-stable-metric-value', 'canonical metric data attribute')
assertIncludes(kpiCardFile, 'StableMetricValue', 'shared StableMetricValue usage')
assertIncludes(kpiCardFile, 'key={`${title}:${String(value)}:${suffix ?? \'\'}', 'deterministic metric identity')
assertIncludes(payrollFile, '}), [backendWorkers, sites])', 'Payroll backend rows depend on sites')
assertIncludes(attendanceLiveFile, 'data-live-kpi-sync', 'AttendanceLive preserved hardened direct KPI rendering')

const forbiddenReloads = [/window\.location\.reload/i, /\blocation\.reload/i, /history\.go\(0\)/i]
const sourceFiles = []
const walk = (dir) => {
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    if (entry.name === 'node_modules' || entry.name === 'dist') continue
    const fullPath = path.join(dir, entry.name)
    if (entry.isDirectory()) {
      walk(fullPath)
      continue
    }
    if (/\.(ts|tsx|js|jsx|mjs)$/.test(entry.name)) sourceFiles.push(fullPath)
  }
}
walk(path.join(root, 'src'))
for (const file of sourceFiles) {
  const text = fs.readFileSync(file, 'utf8')
  for (const pattern of forbiddenReloads) {
    if (pattern.test(text)) findings.push(`${path.relative(root, file)}: forbidden reload hack`)
  }
}

console.log('KPI rendering audit')
console.log(`Checked ${sourceFiles.length} frontend source files`)
if (findings.length) {
  console.log(`Findings: ${findings.length}`)
  findings.forEach((finding) => console.log(finding))
  process.exit(1)
}

console.log('StableMetricValue and shared KpiCard checks passed.')
console.log('Payroll dependency check passed.')
console.log('AttendanceLive hardened KPI rendering is preserved.')
console.log('No full-page reload hacks found.')
