import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { searchInvoices, type InvoiceStatus, type InvoiceSourceType } from '../../../lib/api/invoicing'
import { Button } from '../../../components/ui/Button'
import { NavLink } from 'react-router'

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
        <h1 className="text-xl font-semibold">Facturación Electrónica</h1>
        <NavLink
          to="/app/invoicing/setup"
          className="rounded bg-slate-100 px-3 py-1.5 text-sm font-medium text-slate-700 hover:bg-slate-200"
        >
          Configuración ARCA
        </NavLink>
      </div>

      <div className="flex gap-3">
        <select
          value={status}
          onChange={e => { setStatus(e.target.value as InvoiceStatus | ''); setPage(1) }}
          className="rounded border border-slate-300 px-3 py-1.5 text-sm"
        >
          <option value="">Todos los estados</option>
          <option value="authorized">Autorizadas</option>
          <option value="pending">Pendientes</option>
          <option value="rejected">Rechazadas</option>
        </select>
      </div>

      {isLoading ? (
        <p className="text-sm text-slate-500">Cargando...</p>
      ) : !data?.items.length ? (
        <p className="text-sm text-slate-500">No hay facturas para mostrar.</p>
      ) : (
        <div className="overflow-x-auto rounded border border-slate-200">
          <table className="min-w-full text-sm">
            <thead className="bg-slate-50 text-left text-xs font-medium uppercase text-slate-500">
              <tr>
                <th className="px-4 py-3">Nº</th>
                <th className="px-4 py-3">Tipo</th>
                <th className="px-4 py-3">Origen</th>
                <th className="px-4 py-3">Estado</th>
                <th className="px-4 py-3">Total</th>
                <th className="px-4 py-3">Fecha</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {data.items.map(inv => (
                <tr key={inv.id} className="hover:bg-slate-50">
                  <td className="px-4 py-3 font-mono">
                    {inv.number ?? '—'}
                    {inv.isCredit && (
                      <span className="ml-1 rounded bg-orange-100 px-1 py-0.5 text-xs text-orange-700">
                        NC
                      </span>
                    )}
                  </td>
                  <td className="px-4 py-3 uppercase">Fcta. {inv.invoiceType}</td>
                  <td className="px-4 py-3">{SOURCE_LABELS[inv.sourceType]}</td>
                  <td className="px-4 py-3">
                    <span
                      className={`rounded px-1.5 py-0.5 text-xs font-medium ${
                        inv.status === 'authorized'
                          ? 'bg-green-100 text-green-700'
                          : inv.status === 'rejected'
                            ? 'bg-red-100 text-red-700'
                            : 'bg-slate-100 text-slate-600'
                      }`}
                    >
                      {STATUS_LABELS[inv.status]}
                    </span>
                  </td>
                  <td className="px-4 py-3">$ {inv.totalAmount.toFixed(2)}</td>
                  <td className="px-4 py-3 text-slate-500">
                    {new Date(inv.issuedAt).toLocaleDateString('es-AR')}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {data && data.totalCount > 25 && (
        <div className="flex gap-2">
          <Button
            variant="secondary"
            disabled={page <= 1}
            onClick={() => setPage(p => p - 1)}
          >
            Anterior
          </Button>
          <Button
            variant="secondary"
            disabled={page * 25 >= data.totalCount}
            onClick={() => setPage(p => p + 1)}
          >
            Siguiente
          </Button>
        </div>
      )}
    </div>
  )
}
