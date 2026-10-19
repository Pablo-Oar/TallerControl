import { describe, expect, it } from 'vitest'

const sources = import.meta.glob<string>('../areas/**/*.{ts,tsx}', {
  query: '?raw',
  import: 'default',
  eager: true,
})

describe('T-UI-08 arquitectura de UI (D-24)', () => {
  it('ningún archivo de src/areas importa de components/watermelon', () => {
    const files = Object.keys(sources)
    expect(files.length).toBeGreaterThan(0)
    const offenders = files.filter((f) => /from\s+['"][^'"]*components\/watermelon/.test(sources[f]))
    expect(offenders).toEqual([])
  })
})
