import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { reportsApi } from '../../../lib/api/reports'
import { Button } from '../../../components/ui/Button'
import { StatusBadge } from '../../../components/ui/StatusBadge'
import { DataTable } from '../../../components/ui/DataTable'
import { VehicleLabel } from '../../../components/ui/VehicleLabel'
import { Tabs, TabPanel } from '../../../components/ui/Tabs'
import { inputCls } from '../../../lib/styles'
import { formatDate, formatDateOnly, localDateISO } from '../../../lib/dates'

type Tab = 'revenue' | 'receivables' | 'stock' | 'purchases'

const TAB_LABELS: Record<Tab, string> = {
  revenue: 'Ingresos',
  receivables: 'Por cobrar',
  stock: 'Stock',
  purchases: 'Compras',
}

const todayStr = () => localDateISO()

const WEEKDAYS = ['Domingo', 'Lunes', 'Martes', 'Miércoles', 'Jueves', 'Viernes', 'Sábado']
/** Día de la semana de una fecha «solo día» (yyyy-mm-dd), sin pasar por la zona horaria. */
const weekdayLabel = (date: string) => WEEKDAYS[new Date(`${date.slice(0, 10)}T12:00:00`).getDay()]
const monthAgoStr = () => localDateISO(0, -1)

const fmtARS = (n: number) =>
  new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS', minimumFractionDigits: 2 }).format(n)


function downloadExcel(url: string, filename: string) {
  const a = document.createElement('a')
  a.href = url
  a.download = filename
  a.click()
}

const REVENUE_SOURCE_LABEL: Record<string, string> = {
  cash: 'Efectivo',
  transfer: 'Transferencia',
  debit: 'Débito',
  credit: 'Crédito',
  counter_sale: 'Ventas de mostrador',
}

const PURCHASE_STATUS_LABEL: Record<string, string> = {
  draft: 'Borrador',
  confirmed: 'Confirmada',
  cancelled: 'Cancelada',
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
      <h1 className="text-lg font-bold tracking-wide text-tc-text uppercase">Reportes</h1>

      <Tabs
        label="Reportes"
        idPrefix="reports"
        value={tab}
        onChange={setTab}
        items={(Object.keys(TAB_LABELS) as Tab[]).map(t => ({ id: t, label: TAB_LABELS[t] }))}
      />

      <TabPanel idPrefix="reports" tabId={tab} active>
      {isDateRange && (
        <div className="grid grid-cols-2 gap-3 sm:flex sm:flex-wrap sm:items-center">
          <div className="flex flex-col gap-1 sm:flex-row sm:items-center sm:gap-2">
            <span className="text-[11px] font-bold tracking-wider uppercase text-tc-text-muted">Desde</span>
            <input type="date" value={from} onChange={e => setFrom(e.target.value)} className={`${inputCls} w-full`} />
          </div>
          <div className="flex flex-col gap-1 sm:flex-row sm:items-center sm:gap-2">
            <span className="text-[11px] font-bold tracking-wider uppercase text-tc-text-muted">Hasta</span>
            <input type="date" value={to} onChange={e => setTo(e.target.value)} className={`${inputCls} w-full`} />
          </div>
        </div>
      )}

      {tab === 'revenue' && (
        <ReportSection
          loading={revenueQuery.isLoading}
          error={revenueQuery.isError}
          exportUrl={reportsApi.exportUrl('revenue', from, to)}
          exportName={`ingresos_${from}_${to}.xlsx`}
          summary={revenueQuery.data
            ? `Total: ${fmtARS(revenueQuery.data.total)} (${revenueQuery.data.count} cobros y ventas)`
            : undefined}
        >
          {revenueQuery.data && (revenueQuery.data.breakdown?.length ?? 0) > 0 && (
            <ul className="mb-3 flex flex-wrap gap-2">
              {revenueQuery.data.breakdown!.map(b => (
                <li key={b.source} className="rounded-full border border-tc-border bg-tc-surface px-3 py-1 text-xs text-tc-text-secondary">
                  {REVENUE_SOURCE_LABEL[b.source] ?? b.source}: <b className="tabular-nums text-tc-text">{fmtARS(b.amount)}</b>
                </li>
              ))}
            </ul>
          )}
          {revenueQuery.data && (
            <DataTable
              label="Ingresos por día"
              rows={revenueQuery.data.rows}
              getRowId={r => r.date}
              columns={[
                { key: 'date', header: 'Fecha', widthClass: 'w-32', sortable: true, sortValue: r => r.date, cell: r => <span className="tabular-nums text-tc-text-secondary">{formatDateOnly(r.date)}</span> },
                { key: 'weekday', header: 'Día', widthClass: 'w-28', cell: r => <span className="text-tc-text-secondary">{weekdayLabel(r.date)}</span> },
                { key: 'count', header: 'Cobros', align: 'center', widthClass: 'w-24', sortable: true, sortValue: r => r.paymentCount, cell: r => <span className="tabular-nums text-tc-text-secondary">{r.paymentCount}</span> },
                { key: 'main', header: 'Medio principal', widthClass: 'w-44', cell: r => <span className="text-tc-text-secondary">{r.mainSource ? (REVENUE_SOURCE_LABEL[r.mainSource] ?? r.mainSource) : '—'}</span> },
                { key: 'avg', header: 'Ticket promedio', align: 'right', widthClass: 'w-40', sortable: true, sortValue: r => r.amount / Math.max(r.paymentCount, 1), cell: r => <span className="tabular-nums text-tc-text-secondary">{fmtARS(r.amount / Math.max(r.paymentCount, 1))}</span> },
                {
                  key: 'share',
                  header: '% del total',
                  widthClass: 'w-44',
                  sortable: true,
                  sortValue: r => r.amount,
                  cell: r => {
                    const pct = revenueQuery.data!.total > 0 ? (r.amount / revenueQuery.data!.total) * 100 : 0
                    return (
                      <span className="flex items-center gap-2">
                        <span className="h-1.5 w-20 overflow-hidden rounded-full bg-tc-border" aria-hidden="true">
                          <span className="block h-full rounded-full bg-tc-purple" style={{ width: `${Math.min(pct, 100)}%` }} />
                        </span>
                        <span className="w-12 text-xs tabular-nums text-tc-text-secondary">{pct.toFixed(1).replace('.', ',')}%</span>
                      </span>
                    )
                  },
                },
                { key: 'amount', header: 'Monto', align: 'right', widthClass: 'w-40', sortable: true, sortValue: r => r.amount, cell: r => <span className="font-bold tabular-nums text-tc-purple">{fmtARS(r.amount)}</span> },
              ]}
              renderCard={r => (
                <div className="flex items-baseline justify-between gap-3">
                  <span className="tabular-nums text-sm text-tc-text-secondary">{weekdayLabel(r.date)} {formatDateOnly(r.date)} · {r.paymentCount} cobro{r.paymentCount !== 1 ? 's' : ''}</span>
                  <span className="font-bold tabular-nums text-tc-purple">{fmtARS(r.amount)}</span>
                </div>
              )}
              emptyMessage="Sin cobros en el período."
            />
          )}
        </ReportSection>
      )}

      {tab === 'receivables' && (
        <ReportSection
          loading={receivablesQuery.isLoading}
          error={receivablesQuery.isError}
          exportUrl={reportsApi.exportUrl('receivables')}
          exportName="cuentas_por_cobrar.xlsx"
          summary={receivablesQuery.data
            ? `Total por cobrar: ${fmtARS(receivablesQuery.data.total)}`
            : undefined}
        >
          {receivablesQuery.data && (
            <DataTable
              label="Cuentas por cobrar"
              rows={receivablesQuery.data.rows}
              getRowId={r => r.workOrderId}
              columns={[
                { key: 'number', header: 'N° OT', widthClass: 'w-28', sortable: true, sortValue: r => r.workOrderNumber, cell: r => <span className="font-mono font-bold tabular-nums text-tc-text">#{r.workOrderNumber}</span> },
                { key: 'customer', header: 'Cliente', widthClass: 'w-[22rem]', sortable: true, sortValue: r => r.customerName, cell: r => <span className="text-tc-text-secondary">{r.customerName}</span> },
                { key: 'plate', header: 'Vehículo', widthClass: 'w-56', cell: r => <VehicleLabel plate={r.vehiclePlate} brand={r.vehicleBrand} model={r.vehicleModel} /> },
                { key: 'concept', header: 'Qué se cobra', cell: r => <span className="line-clamp-2 text-tc-text-secondary" title={r.concept ?? undefined}>{r.concept || '—'}</span> },
                {
                  key: 'balance', header: 'Saldo', align: 'right', widthClass: 'w-44', sortable: true, sortValue: r => r.balance,
                  cell: r => (
                    <div>
                      <span className="font-bold tabular-nums text-tc-stamp-red">{fmtARS(r.balance)}</span>
                      {!!r.total && r.total !== r.balance && <p className="text-xs tabular-nums text-tc-text-muted">de {fmtARS(r.total)}</p>}
                    </div>
                  ),
                },
                { key: 'date', header: 'Fecha', align: 'center', widthClass: 'w-36', sortable: true, sortValue: r => r.createdAt, cell: r => <span className="tabular-nums text-tc-text-secondary">{formatDate(r.createdAt)}</span> },
              ]}
              cardStub={r => <span className="text-xs font-bold tabular-nums text-tc-text-secondary">#{r.workOrderNumber}</span>}
              renderCard={r => (
                <>
                  <p className="truncate text-sm text-tc-text">{r.customerName}</p>
                  <p className="text-xs text-tc-text-muted"><VehicleLabel plate={r.vehiclePlate} brand={r.vehicleBrand} model={r.vehicleModel} inline /> · {formatDate(r.createdAt)}</p>
                  {r.concept && <p className="mt-0.5 line-clamp-2 text-xs text-tc-text-secondary">{r.concept}</p>}
                  <p className="mt-1 font-bold tabular-nums text-tc-stamp-red">{fmtARS(r.balance)}</p>
                </>
              )}
              emptyMessage="No hay saldos por cobrar."
            />
          )}
        </ReportSection>
      )}

      {tab === 'stock' && (
        <ReportSection
          loading={stockQuery.isLoading}
          error={stockQuery.isError}
          exportUrl={reportsApi.exportUrl('stock')}
          exportName="stock.xlsx"
          summary={stockQuery.data
            ? `${stockQuery.data.lowStockCount} ${stockQuery.data.lowStockCount === 1 ? 'ítem' : 'ítems'} con stock bajo`
            : undefined}
        >
          {stockQuery.data && (
            <DataTable
              label="Stock"
              rows={stockQuery.data.rows}
              getRowId={r => r.catalogItemId}
              columns={[
                { key: 'name', header: 'Nombre', sortable: true, sortValue: r => r.name, cell: r => <span className="font-medium text-tc-text">{r.name}</span> },
                { key: 'sku', header: 'SKU', widthClass: 'w-32', cell: r => <span className="font-mono text-xs text-tc-text-secondary">{r.sku ?? '—'}</span> },
                { key: 'kind', header: 'Tipo', widthClass: 'w-28', cell: r => <span className="text-xs text-tc-text-secondary">{r.kind.toLowerCase() === 'service' ? 'Servicio' : r.kind.toLowerCase() === 'product' ? 'Producto' : r.kind}</span> },
                { key: 'qty', header: 'Stock', align: 'right', widthClass: 'w-24', sortable: true, sortValue: r => r.stockQty, cell: r => <span className="tabular-nums text-tc-text-secondary">{r.stockQty}</span> },
                { key: 'min', header: 'Mín.', align: 'right', widthClass: 'w-24', cell: r => <span className="tabular-nums text-tc-text-muted">{r.stockMin ?? '—'}</span> },
                { key: 'cost', header: 'Costo unit.', align: 'right', widthClass: 'w-32', sortable: true, sortValue: r => r.unitCost, cell: r => <span className="font-medium tabular-nums text-tc-purple">{r.unitCost > 0 ? fmtARS(r.unitCost) : '—'}</span> },
                { key: 'status', header: 'Estado', align: 'center', widthClass: 'w-40', sortable: true, sortValue: r => (r.isLowStock ? 0 : 1), cell: r => <StatusBadge status={r.isLowStock ? 'low_stock' : 'ok'} label={r.isLowStock ? 'Bajo' : 'OK'} /> },
              ]}
              renderCard={r => (
                <>
                  <p className="truncate text-sm font-medium text-tc-text">{r.name}</p>
                  <p className="text-xs text-tc-text-secondary">Stock {r.stockQty}{r.stockMin != null ? ` · mín. ${r.stockMin}` : ''}</p>
                  <div className="mt-1.5"><StatusBadge status={r.isLowStock ? 'low_stock' : 'ok'} label={r.isLowStock ? 'Bajo' : 'OK'} /></div>
                </>
              )}
              emptyMessage="No hay ítems con control de stock."
            />
          )}
        </ReportSection>
      )}

      {tab === 'purchases' && (
        <ReportSection
          loading={purchasesQuery.isLoading}
          error={purchasesQuery.isError}
          exportUrl={reportsApi.exportUrl('purchases', from, to)}
          exportName={`compras_${from}_${to}.xlsx`}
          summary={purchasesQuery.data
            ? `Total: ${fmtARS(purchasesQuery.data.total)} (${purchasesQuery.data.count} compras)`
            : undefined}
        >
          {purchasesQuery.data && (
            <DataTable
              label="Compras"
              rows={purchasesQuery.data.rows}
              getRowId={r => r.purchaseId}
              columns={[
                { key: 'number', header: 'Nº', widthClass: 'w-24', sortable: true, sortValue: r => r.purchaseNumber, cell: r => <span className="font-mono font-bold tabular-nums text-tc-text">#{r.purchaseNumber}</span> },
                { key: 'supplier', header: 'Proveedor', sortable: true, sortValue: r => r.supplierName ?? '', cell: r => <span className="text-tc-text-secondary">{r.supplierName ?? '—'}</span> },
                { key: 'date', header: 'Fecha', widthClass: 'w-32', sortable: true, sortValue: r => r.date, cell: r => <span className="tabular-nums text-tc-text-secondary">{formatDateOnly(r.date)}</span> },
                { key: 'total', header: 'Total', align: 'right', widthClass: 'w-36', sortable: true, sortValue: r => r.total, cell: r => <span className="font-bold tabular-nums text-tc-purple">{fmtARS(r.total)}</span> },
                { key: 'status', header: 'Estado', widthClass: 'w-32', cell: r => <span className="text-xs text-tc-text-secondary">{PURCHASE_STATUS_LABEL[r.status.toLowerCase()] ?? r.status}</span> },
              ]}
              cardStub={r => <span className="text-xs font-bold tabular-nums text-tc-text-secondary">#{r.purchaseNumber}</span>}
              renderCard={r => (
                <>
                  <p className="truncate text-sm text-tc-text">{r.supplierName ?? '—'}</p>
                  <p className="text-xs tabular-nums text-tc-text-muted">{formatDateOnly(r.date)}</p>
                  <p className="mt-1 font-bold tabular-nums text-tc-purple">{fmtARS(r.total)}</p>
                </>
              )}
              emptyMessage="Sin compras en el período."
            />
          )}
        </ReportSection>
      )}
      </TabPanel>
    </div>
  )
}

function ReportSection({
  loading, error, exportUrl, exportName, summary, children
}: {
  loading: boolean
  error?: boolean
  exportUrl: string
  exportName: string
  summary?: string
  children?: React.ReactNode
}) {
  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center justify-between gap-x-4 gap-y-2">
        {summary && (
          <p className="min-w-0 text-sm font-bold text-tc-text-secondary max-sm:basis-full">{summary}</p>
        )}
        <Button
          variant="secondary"
          size="sm"
          onClick={() => downloadExcel(exportUrl, exportName)}
          className="ml-auto max-sm:w-full"
        >
          Exportar Excel
        </Button>
      </div>
      {error ? (
        <div className="rounded-2xl border border-tc-border bg-white p-1.5">
          <p role="alert" className="rounded-xl bg-red-50 px-4 py-6 text-sm text-red-700">
            No se pudo cargar el reporte. Volvé a intentar en un momento.
          </p>
        </div>
      ) : loading ? (
        <div className="rounded-2xl border border-tc-border bg-white p-1.5" aria-busy="true">
          {[...Array(5)].map((_, i) => (
            <div key={i} className="mb-1.5 h-10 animate-pulse rounded-xl bg-tc-surface last:mb-0" />
          ))}
        </div>
      ) : (
        children
      )}
    </div>
  )
}
