import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
const read = (p) => readFileSync(new URL(`../${p}`, import.meta.url), 'utf8')
const prod = read('docker-compose.prod.yml')
assert(!/^\s*build:/m.test(prod), 'Production must not build from source')
assert(prod.includes('127.0.0.1:8080:8080'))
assert(prod.includes('7000:7000/tcp') && prod.includes('9500:9500/tcp'))
assert(!prod.includes('5432:5432'))
assert(prod.includes('external: true'))
assert(prod.includes('BUILDTRACK_POSTGRES_VOLUME:?'))
assert(prod.includes('BUILDTRACK_INITIALIZE_DATABASE: "false"'))
assert(prod.includes('DAHUA_ACTIVE_REGISTER_PASSWORD_OVERRIDE:-'))
assert(!prod.includes('./backend'))
const client = read('src/shared/api/client.ts')
assert(client.includes("import.meta.env.DEV ? 'http://localhost:8080' : '/backend'"))
const rewrites = JSON.parse(read('vercel.json')).rewrites
assert.equal(rewrites[0].source, '/backend/:path*')
assert.equal(rewrites[0].destination, 'https://api.buildtrack.ferstaclabs.com/:path*')
assert.equal(rewrites.at(-1).destination, '/')
for (const role of ['Api', 'Worker.Dahua']) {
  const dockerfile = read(`backend/src/BuildTrack.${role}/Dockerfile`)
  assert(dockerfile.includes('USER app'))
  assert(!dockerfile.includes('COPY backend/vendor'))
}
console.log('Deployment audit passed: image-only compose, private database, required external volume, non-root images, Vercel API routing.')
