const ars = new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS', maximumFractionDigits: 0 })

export const fmtARS = (n: number) => ars.format(n)

/** «hace 5 h» / «hace 3 días»: para presupuestos enviados y órdenes que llevan tiempo en un estado. */
export function agoLabel(hours: number): string {
  if (hours < 1) return 'hace menos de 1 h'
  if (hours < 48) return `hace ${Math.floor(hours)} h`
  return `hace ${Math.floor(hours / 24)} días`
}

export function daysLabel(days: number): string {
  if (days <= 0) return 'hoy'
  return days === 1 ? '1 día' : `${days} días`
}

/** Hora local del navegador, `14:30`. */
export function timeLabel(iso: string): string {
  return new Date(iso).toLocaleTimeString('es-AR', { hour: '2-digit', minute: '2-digit', hour12: false })
}

/** Día corto para el gráfico, `lun 27` (la fecha ya viene como día local del taller, sin zona). */
export function shortDay(dateISO: string): string {
  const [y, m, d] = dateISO.split('-').map(Number)
  const day = new Date(y, m - 1, d)
  const name = day.toLocaleDateString('es-AR', { weekday: 'short' }).replace('.', '')
  return `${name} ${d}`
}

/**
 * Enlace `wa.me` con el mensaje armado (D-40: el envío automático sigue fuera de alcance).
 * El teléfono del cliente viene en E.164 (`+5491122334455`); `wa.me` quiere solo los dígitos.
 */
export function whatsappUrl(phone: string | null, text: string): string | null {
  const digits = (phone ?? '').replace(/\D/g, '')
  if (digits.length < 8) return null
  return `https://wa.me/${digits}?text=${encodeURIComponent(text)}`
}
