import { useState } from 'react'
import { Link } from 'react-router'
import { confirmDialog, notify } from '../../../components/ui/dialogs'
import { quotesApi } from '../../../lib/api/quotes'
import type { CashStatus, DashboardThresholds, HotQuote, HotQuotes, LowStockItem } from '../../../lib/api/dashboard'
import { actionErrorMessage } from '../../../lib/api/errors'
import { agoLabel, fmtARS, timeLabel, whatsappUrl } from './format'
import { BanknoteIcon, BoxIcon, FlameIcon, WhatsappIcon } from './icons'
import { Pill, StatusStrip, WidgetEmpty, WidgetShell } from './Widgets'

/**
 * «Presupuestos sin respuesta»: enviados hace más de N horas y sin respuesta.
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
    <WidgetShell title="Presupuestos sin respuesta" icon={<FlameIcon />} accent="amber" count={data.total} linkTo="/app/quotes" linkLabel="Ver todos">
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
                  className="inline-flex min-h-11 flex-none items-center gap-1.5 border border-emerald-600 px-2.5 text-[11px] font-bold tracking-wider uppercase text-emerald-700 transition-colors hover:bg-emerald-600 hover:text-white disabled:cursor-not-allowed disabled:opacity-40"
                >
                  <WhatsappIcon className="h-4 w-4" />
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
  const openSessions = data.accounts.filter(a => a.sessionOpenedAt)
  return (
    <WidgetShell title="Caja" icon={<BanknoteIcon />} accent="green" linkTo="/app/cash" linkLabel="Ir a Caja">
      {data.accounts.length === 0 ? (
        <WidgetEmpty>Todavía no hay cuentas de caja.</WidgetEmpty>
      ) : (
        <>
          {openSessions.length > 0 ? (
            <StatusStrip tone="green" icon={<BanknoteIcon />}>
              {openSessions.length === 1
                ? `Caja abierta${openSessions[0].sessionOpenedBy ? ` por ${openSessions[0].sessionOpenedBy}` : ''}`
                : `${openSessions.length} cajas abiertas`}
            </StatusStrip>
          ) : (
            <StatusStrip tone="amber" icon={<BanknoteIcon />}>
              No hay ninguna sesión de caja abierta.
            </StatusStrip>
          )}
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
    <WidgetShell title="Stock bajo" icon={<BoxIcon />} accent="red" count={total} linkTo="/app/catalog" linkLabel="Ver catálogo">
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
