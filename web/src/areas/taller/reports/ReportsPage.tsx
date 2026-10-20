import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { reportsApi } from '../../../lib/api/reports'
import { Button } from '../../../components/ui/Button'
import { StatusBadge } from '../../../components/ui/StatusBadge'
import { Tabs, TabPanel } from '../../../components/ui/Tabs'

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

const fmtARS = (n: number) =>
  new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS', minimumFractionDigits: 2 }).format(n)

const inputCls = 'border border-tc-border bg-white px-3 py-1.5 text-sm outline-none focus:border-tc-border-mid transition-colors'

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
    <div className="space-y-4">
      <h1 className="text-xl font-bold tracking-widest text-slate-900 uppercase">Reportes</h1>

      <Tabs
        label="Reportes"
        idPrefix="reports"
        value={tab}
        onChange={setTab}
        items={(Object.keys(TAB_LABELS) as Tab[]).map(t => ({ id: t, label: TAB_LABELS[t] }))}
      />

      <TabPanel idPrefix="reports" tabId={tab} active>
      {isDateRange && (
        <div className="flex flex-wrap items-center gap-x-3 gap-y-2">
          <div className="flex items-center gap-2">
            <span className="text-[10px] font-bold tracking-wider uppercase text-slate-400">Desde</span>
            <input type="date" value={from} onChange={e => setFrom(e.target.value)} className={inputCls} />
          </div>
          <div className="flex items-center gap-2">
            <span className="text-[10px] font-bold tracking-wider uppercase text-slate-400">Hasta</span>
            <input type="date" value={to} onChange={e => setTo(e.target.value)} className={inputCls} />
          </div>
        </div>
      )}

      {tab === 'revenue' && (
        <ReportSection
          loading={revenueQuery.isLoading}
          exportUrl={reportsApi.exportUrl('revenue', from, to)}
          exportName={`ingresos_${from}_${to}.xlsx`}
          summary={revenueQuery.data
            ? `Total: ${fmtARS(revenueQuery.data.total)} (${revenueQuery.data.count} cobros)`
            : undefined}
        >
          {revenueQuery.data && (
            <table className="w-full text-sm">
              <thead>
                <tr className="bg-tc-stub-bg border-b border-tc-border">
                  <th className="px-4 py-2 text-left text-[10px] font-bold tracking-[0.14em] uppercase text-slate-400">Fecha</th>
                  <th className="px-4 py-2 text-right text-[10px] font-bold tracking-[0.14em] uppercase text-slate-400">Cobros</th>
                  <th className="px-4 py-2 text-right text-[10px] font-bold tracking-[0.14em] uppercase text-slate-400">Monto</th>
                </tr>
              </thead>
              <tbody>
                {revenueQuery.data.rows.map(r => (
                  <tr key={r.date} className="border-b border-tc-border last:border-0 hover:bg-tc-surface">
                    <td className="px-4 py-2 text-slate-700 tabular-nums">{new Date(r.date + 'T00:00:00').toLocaleDateString('es-AR')}</td>
                    <td className="px-4 py-2 text-right tabular-nums text-slate-600">{r.paymentCount}</td>
                    <td className="px-4 py-2 text-right font-bold text-tc-purple tabular-nums">{fmtARS(r.amount)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </ReportSection>
      )}

      {tab === 'receivables' && (
        <ReportSection
          loading={receivablesQuery.isLoading}
          exportUrl={reportsApi.exportUrl('receivables')}
          exportName="cuentas_por_cobrar.xlsx"
          summary={receivablesQuery.data
            ? `Total por cobrar: ${fmtARS(receivablesQuery.data.total)}`
            : undefined}
        >
          {receivablesQuery.data && (
            <table className="w-full text-sm">
              <thead>
                <tr className="bg-tc-stub-bg border-b border-tc-border">
                  <th className="px-4 py-2 text-left text-[10px] font-bold tracking-[0.14em] uppercase text-slate-400">N° OT</th>
                  <th className="px-4 py-2 text-left text-[10px] font-bold tracking-[0.14em] uppercase text-slate-400">Cliente</th>
                  <th className="px-4 py-2 text-left text-[10px] font-bold tracking-[0.14em] uppercase text-slate-400">Vehículo</th>
                  <th className="px-4 py-2 text-right text-[10px] font-bold tracking-[0.14em] uppercase text-slate-400">Saldo</th>
                  <th className="px-4 py-2 text-left text-[10px] font-bold tracking-[0.14em] uppercase text-slate-400">Fecha</th>
                </tr>
              </thead>
              <tbody>
                {receivablesQuery.data.rows.map(r => (
                  <tr key={r.workOrderId} className="border-b border-tc-border last:border-0 hover:bg-tc-surface">
                    <td className="px-4 py-2 font-mono font-bold tabular-nums text-slate-800">#{r.workOrderNumber}</td>
                    <td className="px-4 py-2 text-slate-700">{r.customerName}</td>
                    <td className="px-4 py-2 font-mono tracking-widest text-sm text-slate-600">{r.vehiclePlate}</td>
                    <td className="px-4 py-2 text-right font-bold text-tc-stamp-red tabular-nums">{fmtARS(r.balance)}</td>
                    <td className="px-4 py-2 text-slate-500 tabular-nums">{new Date(r.createdAt).toLocaleDateString('es-AR')}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </ReportSection>
      )}

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
              <thead>
                <tr className="bg-tc-stub-bg border-b border-tc-border">
                  <th className="px-4 py-2 text-left text-[10px] font-bold tracking-[0.14em] uppercase text-slate-400">Nombre</th>
                  <th className="px-4 py-2 text-left text-[10px] font-bold tracking-[0.14em] uppercase text-slate-400">SKU</th>
                  <th className="px-4 py-2 text-left text-[10px] font-bold tracking-[0.14em] uppercase text-slate-400">Tipo</th>
                  <th className="px-4 py-2 text-right text-[10px] font-bold tracking-[0.14em] uppercase text-slate-400">Stock</th>
                  <th className="px-4 py-2 text-right text-[10px] font-bold tracking-[0.14em] uppercase text-slate-400">Mín.</th>
                  <th className="px-4 py-2 text-right text-[10px] font-bold tracking-[0.14em] uppercase text-slate-400">Costo unit.</th>
                  <th className="px-4 py-2 text-left text-[10px] font-bold tracking-[0.14em] uppercase text-slate-400">Estado</th>
                </tr>
              </thead>
              <tbody>
                {stockQuery.data.rows.map(r => (
                  <tr key={r.catalogItemId} className="border-b border-tc-border last:border-0 hover:bg-tc-surface">
                    <td className="px-4 py-2 font-medium text-slate-800">{r.name}</td>
                    <td className="px-4 py-2 font-mono text-slate-500 text-xs">{r.sku ?? '—'}</td>
                    <td className="px-4 py-2 text-slate-500 text-xs">{r.kind}</td>
                    <td className="px-4 py-2 text-right tabular-nums text-slate-700">{r.stockQty}</td>
                    <td className="px-4 py-2 text-right tabular-nums text-slate-400">{r.stockMin ?? '—'}</td>
                    <td className="px-4 py-2 text-right tabular-nums text-tc-purple font-medium">{fmtARS(r.unitCost)}</td>
                    <td className="px-4 py-2">
                      <StatusBadge status={r.isLowStock ? 'low_stock' : 'ok'} label={r.isLowStock ? 'Bajo' : 'OK'} />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </ReportSection>
      )}

      {tab === 'purchases' && (
        <ReportSection
          loading={purchasesQuery.isLoading}
          exportUrl={reportsApi.exportUrl('purchases', from, to)}
          exportName={`compras_${from}_${to}.xlsx`}
          summary={purchasesQuery.data
            ? `Total: ${fmtARS(purchasesQuery.data.total)} (${purchasesQuery.data.count} compras)`
            : undefined}
        >
          {purchasesQuery.data && (
            <table className="w-full text-sm">
              <thead>
                <tr className="bg-tc-stub-bg border-b border-tc-border">
                  <th className="px-4 py-2 text-left text-[10px] font-bold tracking-[0.14em] uppercase text-slate-400">Nº</th>
                  <th className="px-4 py-2 text-left text-[10px] font-bold tracking-[0.14em] uppercase text-slate-400">Proveedor</th>
                  <th className="px-4 py-2 text-left text-[10px] font-bold tracking-[0.14em] uppercase text-slate-400">Fecha</th>
                  <th className="px-4 py-2 text-right text-[10px] font-bold tracking-[0.14em] uppercase text-slate-400">Total</th>
                  <th className="px-4 py-2 text-left text-[10px] font-bold tracking-[0.14em] uppercase text-slate-400">Estado</th>
                </tr>
              </thead>
              <tbody>
                {purchasesQuery.data.rows.map(r => (
                  <tr key={r.purchaseId} className="border-b border-tc-border last:border-0 hover:bg-tc-surface">
                    <td className="px-4 py-2 font-mono font-bold tabular-nums text-slate-800">#{r.purchaseNumber}</td>
                    <td className="px-4 py-2 text-slate-700">{r.supplierName ?? '—'}</td>
                    <td className="px-4 py-2 tabular-nums text-slate-600">{new Date(r.date + 'T00:00:00').toLocaleDateString('es-AR')}</td>
                    <td className="px-4 py-2 text-right font-bold text-tc-purple tabular-nums">{fmtARS(r.total)}</td>
                    <td className="px-4 py-2 text-xs text-slate-500">{r.status}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </ReportSection>
      )}
      </TabPanel>
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
      <div className="flex items-center justify-between gap-4">
        {summary && (
          <p className="text-sm font-bold text-slate-700">{summary}</p>
        )}
        <Button
          variant="secondary"
          size="sm"
          onClick={() => downloadExcel(exportUrl, exportName)}
          className="ml-auto"
        >
          Exportar Excel
        </Button>
      </div>
      <div className="border border-tc-border bg-white overflow-hidden">
        {loading ? (
          <div className="space-y-0">
            {[...Array(5)].map((_, i) => (
              <div key={i} className="h-10 animate-pulse border-b border-tc-border bg-tc-surface last:border-0" />
            ))}
          </div>
        ) : (
          children
        )}
      </div>
    </div>
  )
}
