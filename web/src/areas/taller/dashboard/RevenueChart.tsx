import { Bar, BarChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import type { RevenueDay } from '../../../lib/api/dashboard'
import { fmtARS, shortDay } from './format'
import { WidgetShell } from './Widgets'

const PURPLE = '#7c3aed'
const PURPLE_LIGHT = '#c4b5fd'

/** Ingresos de los últimos 7 días (cobros + ventas de mostrador). Se carga aparte: recharts no suma al chunk principal (D-41). */
export default function RevenueChart({ days }: { days: RevenueDay[] }) {
  // `fill` por barra: hoy en violeta pleno, el resto más claro.
  const data = days.map((d, i) => ({ label: shortDay(d.date), amount: d.amount, fill: i === days.length - 1 ? PURPLE : PURPLE_LIGHT }))
  const total = days.reduce((sum, d) => sum + d.amount, 0)

  return (
    <WidgetShell title="Ingresos de los últimos 7 días" linkTo="/app/reports" linkLabel="Ver reportes">
      <div className="px-4 pt-3">
        <span className="text-[11px] font-bold tracking-[0.12em] uppercase text-tc-text-secondary">Total de la semana</span>
        <p className="tabular-nums text-xl font-bold text-tc-text">{fmtARS(total)}</p>
      </div>
      <div
        className="h-56 px-2 pb-3"
        role="img"
        aria-label={`Ingresos por día: ${data.map(d => `${d.label} ${fmtARS(d.amount)}`).join(', ')}`}
      >
        <ResponsiveContainer width="100%" height="100%">
          <BarChart data={data} margin={{ top: 12, right: 8, left: 8, bottom: 0 }}>
            <XAxis dataKey="label" tickLine={false} axisLine={false} tick={{ fontSize: 11, fill: '#64748b' }} />
            <YAxis hide domain={[0, (max: number) => Math.max(max, 1)]} />
            <Tooltip
              cursor={{ fill: 'rgba(124,58,237,0.08)' }}
              formatter={(value) => [fmtARS(Number(value)), 'Ingresos']}
              contentStyle={{ border: '1px solid #e2e8f0', borderRadius: 0, fontSize: 12 }}
            />
            <Bar dataKey="amount" maxBarSize={44} isAnimationActive={false} />
          </BarChart>
        </ResponsiveContainer>
      </div>
    </WidgetShell>
  )
}
