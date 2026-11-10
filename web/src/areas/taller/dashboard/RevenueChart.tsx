import type { RevenueDay } from '../../../lib/api/dashboard'
import { Tooltip } from '../../../components/ui/Tooltip'
import { fmtARS, shortDay } from './format'
import { ChartIcon } from './icons'
import { WidgetShell } from './Widgets'

/** Ingresos de los últimos 7 días (cobros + ventas de mostrador): gráfico de barras propio, sin biblioteca (D-41 revisada). */
export default function RevenueChart({ days }: { days: RevenueDay[] }) {
  const total = days.reduce((sum, d) => sum + d.amount, 0)
  const max = Math.max(1, ...days.map((d) => d.amount))
  const summary = days.map((d) => `${shortDay(d.date)} ${fmtARS(d.amount)}`).join(', ')

  return (
    <WidgetShell title="Ingresos de los últimos 7 días" icon={<ChartIcon />} accent="purple" linkTo="/app/reports" linkLabel="Ver reportes">
      <div className="px-4 pt-3">
        <span className="text-[11px] font-bold tracking-[0.12em] uppercase text-tc-text-secondary">Total de la semana</span>
        <p className="tabular-nums text-xl font-bold text-tc-text">{fmtARS(total)}</p>
      </div>
      <div className="px-4 pb-4 pt-3" role="img" aria-label={`Ingresos por día: ${summary}`}>
        <div className="flex h-44 items-end gap-2 border-b border-tc-border sm:gap-3">
          {days.map((d, i) => {
            const today = i === days.length - 1
            const height = d.amount > 0 ? Math.max(4, (d.amount / max) * 100) : 0
            return (
              <div key={d.date} className="flex h-full min-w-0 flex-1 flex-col items-center justify-end">
                <Tooltip content={`${shortDay(d.date)}: ${fmtARS(d.amount)}`} focusable className="h-full w-full items-end justify-center">
                  <span
                    aria-hidden="true"
                    style={{ height: `${height}%` }}
                    className={`block w-full max-w-11 rounded-t-md transition-colors ${today ? 'bg-tc-purple' : 'bg-tc-purple/40 hover:bg-tc-purple/70'}`}
                  />
                </Tooltip>
              </div>
            )
          })}
        </div>
        <div className="mt-1.5 flex gap-2 sm:gap-3" aria-hidden="true">
          {days.map((d, i) => (
            <span
              key={d.date}
              className={`min-w-0 flex-1 truncate text-center text-[11px] ${i === days.length - 1 ? 'font-bold text-tc-text' : 'text-tc-text-muted'}`}
            >
              {shortDay(d.date)}
            </span>
          ))}
        </div>
      </div>
    </WidgetShell>
  )
}
