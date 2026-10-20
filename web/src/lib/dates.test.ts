import { describe, expect, it } from 'vitest'
import { dateOnlyToUtcIso, formatDateOnly, localDateISO } from './dates'

describe('dates', () => {
  it('formatDateOnly no corre el día (T-UI-DATE)', () => {
    expect(formatDateOnly('2026-11-04')).toBe('4/11/2026')
    expect(formatDateOnly('2026-11-04T00:00:00Z')).toBe('4/11/2026')
    expect(formatDateOnly(null)).toBe('')
  })

  it('dateOnlyToUtcIso devuelve un instante UTC (offset 0) en el mismo día', () => {
    expect(dateOnlyToUtcIso('2026-10-20')).toBe('2026-10-20T12:00:00.000Z')
  })

  it('localDateISO usa los componentes locales', () => {
    expect(localDateISO()).toMatch(/^\d{4}-\d{2}-\d{2}$/)
    expect(localDateISO(15) > localDateISO()).toBe(true)
  })
})
