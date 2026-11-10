/// <reference types="node" />
import { describe, expect, it } from 'vitest'
import { readFileSync } from 'node:fs'

// T-UI-14: los pares de color de los tokens cumplen WCAG AA (texto 4,5:1; bordes de control 3:1).


const css = readFileSync('src/styles/index.css', 'utf8')

function token(name: string): string {
  const m = css.match(new RegExp(`--color-tc-${name}:[ ]*(#[0-9a-fA-F]{6})`))
  if (!m) throw new Error(`Falta el token ${name}`)
  return m[1]
}

function lum(hex: string): number {
  const c = [1, 3, 5].map((i) => parseInt(hex.slice(i, i + 2), 16) / 255)
  const [r, g, b] = c.map((x) => (x <= 0.03928 ? x / 12.92 : ((x + 0.055) / 1.055) ** 2.4))
  return 0.2126 * r + 0.7152 * g + 0.0722 * b
}

function ratio(a: string, b: string): number {
  const [hi, lo] = [lum(a), lum(b)].sort((x, y) => y - x)
  return (hi + 0.05) / (lo + 0.05)
}

describe('T-UI-14 contraste de tokens', () => {
  const grounds = ['white', 'surface', 'stub-bg']
  it.each(grounds)('texto principal y secundario sobre %s', (g) => {
    expect(ratio(token('text'), token(g))).toBeGreaterThanOrEqual(4.5)
    expect(ratio(token('text-secondary'), token(g))).toBeGreaterThanOrEqual(4.5)
  })
  it.each(['white', 'surface'])('texto atenuado sobre %s', (g) => {
    expect(ratio(token('text-muted'), token(g))).toBeGreaterThanOrEqual(4.5)
  })
  it('borde de campos sobre blanco ≥ 3:1', () => {
    expect(ratio(token('border-input'), token('white'))).toBeGreaterThanOrEqual(3)
  })
  it.each(['success', 'warning', 'danger', 'info', 'neutral'])('familia %s: texto sobre su fondo', (f) => {
    expect(ratio(token(`${f}-text`), token(`${f}-bg`))).toBeGreaterThanOrEqual(4.5)
  })
  it.each(['success', 'warning', 'danger', 'info'])('familia %s: sólido con texto blanco', (f) => {
    expect(ratio('#ffffff', token(`${f}-solid`))).toBeGreaterThanOrEqual(4.5)
  })
  it('texto sobre la franja navy', () => {
    for (const n of ['navy', 'navy-surface', 'navy-deep'])
      expect(ratio(token('navy-text-dim'), token(n))).toBeGreaterThanOrEqual(4.5)
  })
})
