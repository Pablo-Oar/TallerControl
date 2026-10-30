import { useState } from 'react'
import { Link } from 'react-router'
import { confirmDialog, notify } from '../../../components/ui/dialogs'
import { quotesApi } from '../../../lib/api/quotes'
import type { CashStatus, DashboardThresholds, HotQuote, HotQuotes, LowStockItem } from '../../../lib/api/dashboard'
import { actionErrorMessage } from '../../../lib/api/errors'
import { agoLabel, fmtARS, timeLabel, whatsappUrl } from './format'
import { Pill, WidgetEmpty, WidgetShell } from './Widgets'

/**
 * «Presupuestos calientes»: enviados hace más de N horas y sin respuesta.
 * «Reenviar por WhatsApp» (D-40) emite un enlace nuevo —el anterior deja de funcionar— y abre `wa.me` con el mensaje armado.
 */
export function HotQuotesWidget({ data, thresholds }: { data: HotQuotes; thresholds: DashboardThresholds }) {
  const [busyId, setBusyId] = useState<string | null>(null)

  async function resend(q: HotQuote) {
    if (!q.customerPhone) return
    const ok = await confirmDialog(
      `Se genera un enlace nuevo del presupuesto #${q.number} y el anterior deja de funcionar. ¿Reenviarlo por WhatsApp?`,
      { confirmLabel: 'Reenviar' },
    )
    if (!ok) return

    setBusyId(q.id)
    try {
      const link = await quotesApi.createPublicLink(q.id)
      const url = `${window.location.origin}/p/${link.tokenBase64Url}`
      const text =
        `Hola ${q.customerName}, te escribimos del taller por el presupuesto #${q.number} ` +
        `de tu vehículo ${q.vehiclePlate} (${fmtARS(q.total)}). Podés verlo y aprobarlo acá: ${url}`
      const wa = whatsappUrl(q.customerPhone, text)
      if (wa) window.open(wa, '_blank', 'noopener')
    } catch (e) {
      notify(actionErrorMessage(e, 'No se pudo generar el enlace del presupuesto.'))
    } finally {
      setBusyId(null)
    }
  }

  return (
    <WidgetShell title="Presupuestos calientes" count={data.total} linkTo="/app/quotes" linkLabel="Ver todos">
      {data.items.length === 0 ? (
        <WidgetEmpty>
          Ningún presupuesto enviado lleva más de {thresholds.hotQuoteHours} h sin respuesta.
        </WidgetEmpty>
      ) : (
        <ul className="divide-y divide-tc-border">
          {data.items.map(q => (
            <li key={q.id} className="flex min-h-11 items-center justify-between gap-3 px-4 py-2.5">
              <Link to={`/app/quotes/${q.id}`} className="flex min-h-11 min-w-0 flex-col justify-center hover:underline">
                <span className="block truncate text-sm font-medium text-tc-text">#{q.number} · {q.customerName}</span>
                <span className="block truncate text-xs text-tc-text-muted">
                  <span className="font-mono tracking-widest">{q.vehiclePlate}</span> · {fmtARS(q.total)} · {agoLabel(q.hoursSinceSent)}
                </span>
              </Link>
              {q.customerPhone ? (
                <button
                  type="button"
                  disabled={busyId === q.id}
                  onClick={() => void resend(q)}
                  className="min-h-11 flex-none border border-tc-border px-2.5 text-[11px] font-bold tracking-wider uppercase text-tc-text transition-colors hover:border-tc-purple hover:text-tc-purple disabled:cursor-not-allowed disabled:opacity-40"
                >
                  {busyId === q.id ? 'Generando…' : 'Reenviar WhatsApp'}
                </button>
              ) : (
                <span className="flex-none text-[11px] text-tc-text-muted">Sin teléfono</span>
              )}
            </li>
          ))}
        </ul>
      )}
      {data.total > data.items.length && (
        <p className="border-t border-tc-border px-4 py-2.5 text-xs text-tc-text-secondary">
          Y {data.total - data.items.length} más sin respuesta.
        </p>
      )}
    </WidgetShell>
  )
}

const KIND_LABELS = { cash: 'Efectivo', bank: 'Banco', digital: 'Billetera' } as const

/** Estado de la caja: saldo por cuenta y quién tiene la sesión abierta. */
export function CashWidget({ data }: { data: CashStatus }) {
  return (
    <WidgetShell title="Caja" linkTo="/app/cash" linkLabel="Ir a Caja">
      {data.accounts.length === 0 ? (
        <WidgetEmpty>Todavía no hay cuentas de caja.</WidgetEmpty>
      ) : (
        <>
          <div className="px-4 pt-3 pb-1">
            <span className="text-[11px] font-bold tracking-[0.12em] uppercase text-tc-text-secondary">Saldo total</span>
            <p className="tabular-nums text-xl font-bold text-tc-text">{fmtARS(data.totalBalance)}</p>
          </div>
          <ul className="divide-y divide-tc-border">
            {data.accounts.map(a => (
              <li key={a.id} className="flex min-h-11 items-center justify-between gap-3 px-4 py-2.5">
                <span className="min-w-0">
                  <span className="block truncate text-sm font-medium text-tc-text">{a.name}</span>
                  <span className="block truncate text-xs text-tc-text-muted">
                    {KIND_LABELS[a.kind]} ·{' '}
                    {a.sessionOpenedAt ? (
                      <span className="text-tc-done-text">
                        abierta{a.sessionOpenedBy ? ` por ${a.sessionOpenedBy}` : ''} desde las {timeLabel(a.sessionOpenedAt)}
                      </span>
                    ) : (
                      'sin sesión abierta'
                    )}
                  </span>
                </span>
                <span className="flex-none text-sm font-bold tabular-nums text-tc-text">{fmtARS(a.balance)}</span>
              </li>
            ))}
          </ul>
        </>
      )}
    </WidgetShell>
  )
}

/** Stock bajo: los más urgentes (el stock negativo cuenta; se vende sin stock con alerta, D-07). */
export function LowStockWidget({ items, total }: { items: LowStockItem[]; total: number }) {
  return (
    <WidgetShell title="Stock bajo" count={total} linkTo="/app/catalog" linkLabel="Ver catálogo">
      {items.length === 0 ? (
        <WidgetEmpty>Todo el stock está por encima del mínimo.</WidgetEmpty>
      ) : (
        <ul className="divide-y divide-tc-border">
          {items.map(i => (
            <li key={i.id} className="flex min-h-11 items-center justify-between gap-3 px-4 py-2.5">
              <span className="min-w-0">
                <span className="block truncate text-sm font-medium text-tc-text">{i.name}</span>
                <span className="block truncate text-xs text-tc-text-muted">
                  {i.sku ? `${i.sku} · ` : ''}
                  mínimo {i.stockMin ?? 0}
                </span>
              </span>
              <span className="flex flex-none items-center gap-3">
                {i.stockQty < 0 ? <Pill tone="danger">{i.stockQty} u.</Pill> : <Pill tone="warn">{i.stockQty} u.</Pill>}
                <Link to="/app/purchases?nueva=1" className="flex min-h-11 items-center text-[11px] font-bold tracking-wider uppercase text-tc-purple hover:underline">
                  Comprar
                </Link>
              </span>
            </li>
          ))}
        </ul>
      )}
    </WidgetShell>
  )
}
