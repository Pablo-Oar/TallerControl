/** Clases repetidas en pantallas que todavía usan controles propios (filtros, renglones de tabla). */
export const labelCls = 'mb-1 block text-xs font-bold tracking-wider uppercase text-tc-text-secondary'
/** Control compacto para barras de filtros. */
export const inputCls =
  'border border-tc-border bg-white px-3 py-1.5 text-sm text-tc-text outline-none focus:border-tc-border-mid transition-colors max-md:min-h-11'
/** Control de ancho completo. */
export const fieldCls =
  'w-full border border-tc-border bg-white px-3 py-2 text-sm text-tc-text outline-none focus:border-tc-border-mid transition-colors'

/** Color de lo pendiente de cobro: rojo si no se cobró nada, naranja si hay cobro parcial, apagado si no se debe nada. */
export function pendingTone(balance: number, total: number | null): string {
  if (balance <= 0) return 'text-tc-text-muted'
  return total != null && balance >= total ? 'font-semibold text-tc-stamp-red' : 'font-semibold text-amber-600'
}
