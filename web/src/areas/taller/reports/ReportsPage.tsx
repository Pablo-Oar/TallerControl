import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { reportsApi } from '../../../lib/api/reports'
import { Button } from '../../../components/ui/Button'

type Tab = 'revenue' | 'receivables' | 'stock' | 'purchases'

const TAB_LABELS: Record<Tab, string> = {
  revenue: 'Ingresos',
  receivables: 'Cuentas por cobrar',
  stock: 'Stock',
  purchases: 'Compras',
}

function todayStr() {
  return new Date().toISOString().slice(0, 10)
}
function monthAgoStr() {
  const d = new Date()
  d.setMonth(d.getMonth() - 1)
  return d.toISOString().slice(0, 10)
}

const inputCls = 'rounded-md border border-slate-300 px-3 py-1.5 text-sm outline-none focus:ring-2 focus:ring-blue-500'

function downloadExcel(url: string, filename: string) {
  const a = document.createElement('a')
  a.href = url
  a.download = filename
  a.click()
}

export function ReportsPage() {
  const [tab, setTab] = useState<Tab>('revenue')
  const [from, setFrom] = useState(monthAgoStr)
  const [to, setTo] = useState(todayStr)

  const revenueQuery = useQuery({
    queryKey: ['report-revenue', from, to],
    queryFn: () => reportsApi.getRevenue(from, to),
    enabled: tab === 'revenue',
  })
  const receivablesQuery = useQuery({
    queryKey: ['report-receivables'],
    queryFn: () => reportsApi.getReceivables(),
    enabled: tab === 'receivables',
  })
  const stockQuery = useQuery({
    queryKey: ['report-stock'],
    queryFn: () => reportsApi.getStock(),
    enabled: tab === 'stock',
  })
  const purchasesQuery = useQuery({
    queryKey: ['report-purchases', from, to],
    queryFn: () => reportsApi.getPurchases(from, to),
    enabled: tab === 'purchases',
  })

  const isDateRange = tab === 'revenue' || tab === 'purchases'

  return (
    <div className="p-6 space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold text-slate-800">Reportes</h1>
      </div>

      {/* Tabs */}
      <div className="flex gap-1 border-b border-slate-200">
        {(Object.keys(TAB_LABELS) as Tab[]).map(t => (
          <button
            key={t}
            onClick={() => setTab(t)}
            className={`px-4 py-2 text-sm font-medium border-b-2 transition-colors ${
              tab === t
                ? 'border-blue-600 text-blue-700'
                : 'border-transparent text-slate-600 hover:text-slate-800'
            }`}
          >
            {TAB_LABELS[t]}
          </button>
        ))}
      </div>

      {/* Filtros de fecha */}
      {isDateRange && (
        <div className="flex items-center gap-3 flex-wrap">
          <label className="text-sm font-medium text-slate-700">Desde</label>
          <input type="date" value={from} onChange={e => setFrom(e.target.value)} className={inputCls} />
          <label className="text-sm font-medium text-slate-700">Hasta</label>
          <input type="date" value={to} onChange={e => setTo(e.target.value)} className={inputCls} />
        </div>
      )}

      {/* Revenue */}
      {tab === 'revenue' && (
        <ReportSection
          loading={revenueQuery.isLoading}
          exportUrl={reportsApi.exportUrl('revenue', from, to)}
          exportName={`ingresos_${from}_${to}.xlsx`}
          summary={revenueQuery.data
            ? `Total: $${revenueQuery.data.total.toLocaleString('es-AR', { minimumFractionDigits: 2 })} (${revenueQuery.data.count} cobros)`
            : undefined}
        >
          {revenueQuery.data && (
            <table className="w-full text-sm">
              <thead className="bg-slate-50 text-left text-xs font-semibold uppercase tracking-wide text-slate-500">
                <tr>
                  <th className="px-4 py-2">Fecha</th>
                  <th className="px-4 py-2 text-right">Cobros</th>
                  <th className="px-4 py-2 text-right">Monto</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {revenueQuery.data.rows.map(r => (
                  <tr key={r.date}>
                    <td className="px-4 py-2">{new Date(r.date + 'T00:00:00').toLocaleDateString('es-AR')}</td>
                    <td className="px-4 py-2 text-right">{r.paymentCount}</td>
                    <td className="px-4 py-2 text-right font-medium">
                      ${r.amount.toLocaleString('es-AR', { minimumFractionDigits: 2 })}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </ReportSection>
      )}

      {/* Receivables */}
      {tab === 'receivables' && (
        <ReportSection
          loading={receivablesQuery.isLoading}
          exportUrl={reportsApi.exportUrl('receivables')}
          exportName="cuentas_por_cobrar.xlsx"
          summary={receivablesQuery.data
            ? `Total por cobrar: $${receivablesQuery.data.total.toLocaleString('es-AR', { minimumFractionDigits: 2 })}`
            : undefined}
        >
          {receivablesQuery.data && (
            <table className="w-full text-sm">
              <thead className="bg-slate-50 text-left text-xs font-semibold uppercase tracking-wide text-slate-500">
                <tr>
                  <th className="px-4 py-2">N° OT</th>
                  <th className="px-4 py-2">Cliente</th>
                  <th className="px-4 py-2">Vehículo</th>
                  <th className="px-4 py-2 text-right">Saldo</th>
                  <th className="px-4 py-2">Fecha</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {receivablesQuery.data.rows.map(r => (
                  <tr key={r.workOrderId}>
                    <td className="px-4 py-2">#{r.workOrderNumber}</td>
                    <td className="px-4 py-2">{r.customerName}</td>
                    <td className="px-4 py-2">{r.vehiclePlate}</td>
                    <td className="px-4 py-2 text-right font-medium text-red-600">
                      ${r.balance.toLocaleString('es-AR', { minimumFractionDigits: 2 })}
                    </td>
                    <td className="px-4 py-2 text-slate-500">
                      {new Date(r.createdAt).toLocaleDateString('es-AR')}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </ReportSection>
      )}

      {/* Stock */}
      {tab === 'stock' && (
        <ReportSection
          loading={stockQuery.isLoading}
          exportUrl={reportsApi.exportUrl('stock')}
          exportName="stock.xlsx"
          summary={stockQuery.data
            ? `${stockQuery.data.lowStockCount} ítems con stock bajo`
            : undefined}
        >
          {stockQuery.data && (
            <table className="w-full text-sm">
              <thead className="bg-slate-50 text-left text-xs font-semibold uppercase tracking-wide text-slate-500">
                <tr>
                  <th className="px-4 py-2">Nombre</th>
                  <th className="px-4 py-2">SKU</th>
                  <th className="px-4 py-2">Tipo</th>
                  <th className="px-4 py-2 text-right">Stock actual</th>
                  <th className="px-4 py-2 text-right">Mín.</th>
                  <th className="px-4 py-2 text-right">Costo unit.</th>
                  <th className="px-4 py-2">Estado</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {stockQuery.data.rows.map(r => (
                  <tr key={r.catalogItemId}>
                    <td className="px-4 py-2 font-medium">{r.name}</td>
                    <td className="px-4 py-2 text-slate-500">{r.sku ?? '—'}</td>
                    <td className="px-4 py-2 text-slate-500">{r.kind}</td>
                    <td className="px-4 py-2 text-right">{r.stockQty}</td>
                    <td className="px-4 py-2 text-right text-slate-400">{r.stockMin ?? '—'}</td>
                    <td className="px-4 py-2 text-right">
                      ${r.unitCost.toLocaleString('es-AR', { minimumFractionDigits: 2 })}
                    </td>
                    <td className="px-4 py-2">
                      {r.isLowStock
                        ? <span className="rounded-full bg-red-100 px-2 py-0.5 text-xs font-medium text-red-700">Bajo</span>
                        : <span className="rounded-full bg-green-100 px-2 py-0.5 text-xs font-medium text-green-700">OK</span>}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </ReportSection>
      )}

      {/* Purchases */}
      {tab === 'purchases' && (
        <ReportSection
          loading={purchasesQuery.isLoading}
          exportUrl={reportsApi.exportUrl('purchases', from, to)}
          exportName={`compras_${from}_${to}.xlsx`}
          summary={purchasesQuery.data
            ? `Total: $${purchasesQuery.data.total.toLocaleString('es-AR', { minimumFractionDigits: 2 })} (${purchasesQuery.data.count} compras)`
            : undefined}
        >
          {purchasesQuery.data && (
            <table className="w-full text-sm">
              <thead className="bg-slate-50 text-left text-xs font-semibold uppercase tracking-wide text-slate-500">
                <tr>
                  <th className="px-4 py-2">N°</th>
                  <th className="px-4 py-2">Proveedor</th>
                  <th className="px-4 py-2">Fecha</th>
                  <th className="px-4 py-2 text-right">Total</th>
                  <th className="px-4 py-2">Estado</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {purchasesQuery.data.rows.map(r => (
                  <tr key={r.purchaseId}>
                    <td className="px-4 py-2">#{r.purchaseNumber}</td>
                    <td className="px-4 py-2">{r.supplierName ?? '—'}</td>
                    <td className="px-4 py-2">{new Date(r.date + 'T00:00:00').toLocaleDateString('es-AR')}</td>
                    <td className="px-4 py-2 text-right font-medium">
                      ${r.total.toLocaleString('es-AR', { minimumFractionDigits: 2 })}
                    </td>
                    <td className="px-4 py-2 text-slate-500">{r.status}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </ReportSection>
      )}
    </div>
  )
}

function ReportSection({
  loading, exportUrl, exportName, summary, children
}: {
  loading: boolean
  exportUrl: string
  exportName: string
  summary?: string
  children?: React.ReactNode
}) {
  return (
    <div className="space-y-3">
      <div className="flex items-center justify-between">
        {summary && <p className="text-sm font-medium text-slate-700">{summary}</p>}
        <Button
          variant="secondary"
          onClick={() => downloadExcel(exportUrl, exportName)}
          className="ml-auto"
        >
          Exportar Excel
        </Button>
      </div>
      <div className="rounded-xl border border-slate-200 bg-white overflow-hidden shadow-sm">
        {loading ? (
          <div className="flex items-center justify-center py-10">
            <div className="h-6 w-6 animate-spin rounded-full border-2 border-blue-600 border-t-transparent" />
          </div>
        ) : (
          children
        )}
      </div>
    </div>
  )
}
