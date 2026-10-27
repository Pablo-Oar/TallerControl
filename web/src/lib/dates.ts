/** Fechas "solo día" (YYYY-MM-DD) sin pasar por `new Date(string)`, que las toma como UTC y las corre un día en Argentina. */

const pad = (n: number) => String(n).padStart(2, '0')

/** Hoy (o hoy + `days`) en la fecha LOCAL del dispositivo, como YYYY-MM-DD. */
export function localDateISO(days = 0, months = 0): string {
  const d = new Date()
  if (months) d.setMonth(d.getMonth() + months)
  if (days) d.setDate(d.getDate() + days)
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`
}

/** "2026-11-04" (o "2026-11-04T00:00:00Z") → "4/11/2026", sin corrimiento de zona horaria. */
export function formatDateOnly(value: string | null | undefined): string {
  if (!value) return ''
  const m = /^(\d{4})-(\d{2})-(\d{2})/.exec(value)
  if (!m) return formatDate(value)
  return formatDate(new Date(Number(m[1]), Number(m[2]) - 1, Number(m[3])))
}

/** YYYY-MM-DD → instante UTC (mediodía) para campos `DateTimeOffset` del API: Npgsql exige offset 0. */
export function dateOnlyToUtcIso(value: string): string {
  return new Date(`${value}T12:00:00Z`).toISOString()
}

/** Instante → valor de un `<input type="datetime-local">` en la hora LOCAL del dispositivo (no en UTC). */
export function toDateTimeLocal(iso: string): string {
  const d = new Date(iso)
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`
}

/** Lunes de la semana de una fecha YYYY-MM-DD (semana de lunes a domingo). */
export function mondayOf(dateISO: string): string {
  const [y, m, d] = dateISO.split('-').map(Number)
  const date = new Date(y, m - 1, d)
  const back = (date.getDay() + 6) % 7
  date.setDate(date.getDate() - back)
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`
}

/** Suma días a una fecha YYYY-MM-DD. */
export function addDaysISO(dateISO: string, days: number): string {
  const [y, m, d] = dateISO.split('-').map(Number)
  const date = new Date(y, m - 1, d + days)
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`
}

/** Fecha y hora en el mismo formato en toda la app: 04/11/2026 14:30 (24 h, ceros a la izquierda). */
export function formatDateTime(value: string | Date | null | undefined): string {
  if (!value) return ''
  return new Date(value)
    .toLocaleString('es-AR', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit', hour12: false })
    .replace(',', '')
}

/** Solo fecha: 04/11/2026. */
export function formatDate(value: string | Date | null | undefined): string {
  if (!value) return ''
  return new Date(value).toLocaleDateString('es-AR', { day: '2-digit', month: '2-digit', year: 'numeric' })
}
