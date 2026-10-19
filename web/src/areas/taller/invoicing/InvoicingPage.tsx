import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { searchInvoices, type InvoiceStatus, type InvoiceSourceType } from '../../../lib/api/invoicing'
import { StatusBadge } from '../../../components/ui/StatusBadge'
import { NavLink } from 'react-router'

const fmtARS = (n: number) =>
  new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS', minimumFractionDigits: 2 }).format(n)

const STATUS_LABELS: Record<InvoiceStatus, string> = {
  pending: 'Pendiente',
  authorized: 'Autorizada',
  rejected: 'Rechazada',
}

const SOURCE_LABELS: Record<InvoiceSourceType, string> = {
  work_order: 'Orden de Trabajo',
  counter_sale: 'Venta Mostrador',
}

export function InvoicingPage() {
  const [status, setStatus] = useState<InvoiceStatus | ''>('')
  const [page, setPage] = useState(1)

  const { data, isLoading } = useQuery({
    queryKey: ['invoices', status, page],
    queryFn: () => searchInvoices({ status: status || undefined, page, pageSize: 25 }),
  })

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-bold tracking-widest text-slate-900 uppercase">Facturación Electrónica</h1>
        <NavLink
          to="/app/invoicing/setup"
          className="border border-tc-border bg-white px-3 py-1.5 text-xs font-bold tracking-wider uppercase text-slate-600 hover:bg-tc-surface transition-colors"
        >
          Config. ARCA
        </NavLink>
      </div>

      <div className="flex gap-2">
        <select
          value={status}
          onChange={e => { setStatus(e.target.value as InvoiceStatus | ''); setPage(1) }}
          className="border border-tc-border bg-white px-3 py-1.5 text-sm outline-none focus:border-tc-border-mid transition-colors"
        >
          <option value="">Todos los estados</option>
          <option value="authorized">Autorizadas</option>
          <option value="pending">Pendientes</option>
          <option value="rejected">Rechazadas</option>
        </select>
      </div>

      {isLoading ? (
        <div className="border border-tc-border bg-white">
          {[...Array(5)].map((_, i) => (
            <div key={i} className="h-10 animate-pulse border-b border-tc-border bg-tc-surface last:border-0" />
          ))}
        </div>
      ) : !data?.items.length ? (
        <div className="border border-dashed border-tc-border px-4 py-10 text-center text-sm text-slate-400">
          No hay facturas para mostrar.
        </div>
      ) : (
        <div className="border border-tc-border bg-white overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="bg-tc-stub-bg border-b border-tc-border">
                <th className="px-4 py-2 text-left text-[10px] font-bold tracking-[0.14em] uppercase text-slate-400">Nº</th>
                <th className="px-4 py-2 text-left text-[10px] font-bold tracking-[0.14em] uppercase text-slate-400">Tipo</th>
                <th className="px-4 py-2 text-left text-[10px] font-bold tracking-[0.14em] uppercase text-slate-400">Origen</th>
                <th className="px-4 py-2 text-left text-[10px] font-bold tracking-[0.14em] uppercase text-slate-400">Estado</th>
                <th className="px-4 py-2 text-right text-[10px] font-bold tracking-[0.14em] uppercase text-slate-400">Total</th>
                <th className="px-4 py-2 text-left text-[10px] font-bold tracking-[0.14em] uppercase text-slate-400">Fecha</th>
              </tr>
            </thead>
            <tbody>
              {data.items.map(inv => (
                <tr key={inv.id} className="border-b border-tc-border last:border-0 hover:bg-tc-surface">
                  <td className="px-4 py-2.5 font-mono text-slate-800">
                    {inv.number ?? '—'}
                    {inv.isCredit && (
                      <span className="ml-2 border border-amber-300 px-1 py-0.5 text-[10px] font-bold tracking-wider uppercase text-amber-700">
                        NC
                      </span>
                    )}
                  </td>
                  <td className="px-4 py-2.5 font-mono text-xs uppercase text-slate-600">Fcta. {inv.invoiceType}</td>
                  <td className="px-4 py-2.5 text-slate-600 text-xs">{SOURCE_LABELS[inv.sourceType]}</td>
                  <td className="px-4 py-2.5">
                    <StatusBadge status={inv.status} label={STATUS_LABELS[inv.status]} />
                  </td>
                  <td className="px-4 py-2.5 text-right font-bold text-tc-purple tabular-nums">{fmtARS(inv.totalAmount)}</td>
                  <td className="px-4 py-2.5 text-slate-500 tabular-nums">
                    {new Date(inv.issuedAt).toLocaleDateString('es-AR')}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>

          {data && data.totalCount > 25 && (
            <div className="flex items-center gap-1 border-t border-tc-border px-4 py-2">
              <button
                onClick={() => setPage(p => p - 1)}
                disabled={page <= 1}
                className="border border-tc-border bg-white px-3 py-1 text-xs disabled:opacity-40 hover:bg-tc-surface"
              >
                ← Anterior
              </button>
              <span className="border border-tc-border bg-tc-stub-bg px-3 py-1 text-xs tabular-nums">
                Pág. {page}
              </span>
              <button
                onClick={() => setPage(p => p + 1)}
                disabled={page * 25 >= data.totalCount}
                className="border border-tc-border bg-white px-3 py-1 text-xs disabled:opacity-40 hover:bg-tc-surface"
              >
                Siguiente →
              </button>
            </div>
          )}
        </div>
      )}
    </div>
  )
}
