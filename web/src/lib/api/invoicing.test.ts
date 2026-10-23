import { describe, expect, it } from 'vitest'
import { daysToExpire } from './invoicing'

describe('daysToExpire', () => {
  const now = new Date('2026-10-22T12:00:00Z').getTime()

  it('cuenta los días que faltan, redondeando hacia arriba', () => {
    expect(daysToExpire({ expiresAt: '2026-11-21T12:00:00Z' }, now)).toBe(30)
    expect(daysToExpire({ expiresAt: '2026-10-23T00:00:00Z' }, now)).toBe(1)
  })

  it('es negativo cuando ya venció y nulo si no hay fecha', () => {
    expect(daysToExpire({ expiresAt: '2026-10-01T12:00:00Z' }, now)).toBeLessThan(0)
    expect(daysToExpire({ expiresAt: null }, now)).toBeNull()
  })
})
