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
  if (!m) return new Date(value).toLocaleDateString('es-AR')
  return new Date(Number(m[1]), Number(m[2]) - 1, Number(m[3])).toLocaleDateString('es-AR')
}

/** YYYY-MM-DD → instante UTC (mediodía) para campos `DateTimeOffset` del API: Npgsql exige offset 0. */
export function dateOnlyToUtcIso(value: string): string {
  return new Date(`${value}T12:00:00Z`).toISOString()
}
