import { describe, expect, it } from 'vitest'
import { agoLabel, daysLabel, shortDay, whatsappUrl } from './format'

describe('format del Inicio', () => {
  it('whatsappUrl deja solo los dígitos del teléfono y codifica el mensaje', () => {
    const url = whatsappUrl('+54 9 11 2233-4455', 'Hola, ¿viste el presupuesto?')
    expect(url).toBe(`https://wa.me/5491122334455?text=${encodeURIComponent('Hola, ¿viste el presupuesto?')}`)
  })

  it('whatsappUrl devuelve null si no hay teléfono utilizable', () => {
    expect(whatsappUrl(null, 'hola')).toBeNull()
    expect(whatsappUrl('12345', 'hola')).toBeNull()
  })

  it('agoLabel usa horas hasta 48 h y días después', () => {
    expect(agoLabel(0.2)).toBe('hace menos de 1 h')
    expect(agoLabel(5)).toBe('hace 5 h')
    expect(agoLabel(72)).toBe('hace 3 días')
  })

  it('daysLabel distingue hoy, 1 día y N días', () => {
    expect(daysLabel(0)).toBe('hoy')
    expect(daysLabel(1)).toBe('1 día')
    expect(daysLabel(9)).toBe('9 días')
  })

  it('shortDay arma «día número» sin depender de la zona horaria', () => {
    expect(shortDay('2026-10-30')).toMatch(/30$/)
  })
})
