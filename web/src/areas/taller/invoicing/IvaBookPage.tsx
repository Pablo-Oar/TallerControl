import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { getIvaBook, downloadCitiExport } from '../../../lib/api/invoicing'
import { Button } from '../../../components/ui/Button'
import { InvoicingTabs } from './InvoicingTabs'

const fmtARS = (n: number) =>
  new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS', minimumFractionDigits: 2 }).format(n)

const selectCls =
  'border border-tc-border bg-white px-3 py-1.5 text-sm outline-none focus:border-tc-border-mid transition-colors'

const MONTHS = [
  'Enero', 'Febrero', 'Marzo', 'Abril', 'Mayo', 'Junio',
  'Julio', 'Agosto', 'Septiembre', 'Octubre', 'Noviembre', 'Diciembre',
]

export function IvaBookPage() {
  const now = new Date()
  const [year, setYear] = useState(now.getFullYear())
  const [month, setMonth] = useState(now.getMonth() + 1)
  const [downloading, setDownloading] = useState(false)

  const { data, isLoading, refetch } = useQuery({
    queryKey: ['iva-book', year, month],
    queryFn: () => getIvaBook(year, month),
  })

  async function handleDownloadCiti() {
    setDownloading(true)
    try {
      await downloadCitiExport(year, month)
    } finally {
      setDownloading(false)
    }
  }

  return (
    <div className="space-y-6">
      <h1 className="text-xl font-bold tracking-widest text-tc-text uppercase">Libro IVA Digital</h1>

      <InvoicingTabs />

      <div className="flex justify-end">
        <Button variant="secondary" size="sm" loading={downloading} onClick={handleDownloadCiti}>
          Descargar CITI (.zip)
        </Button>
      </div>

      <div className="flex items-center gap-2 flex-wrap">
        <select value={month} onChange={e => setMonth(Number(e.target.value))} className={selectCls}>
          {MONTHS.map((m, i) => (
            <option key={i + 1} value={i + 1}>{m}</option>
          ))}
        </select>
        <select value={year} onChange={e => setYear(Number(e.target.value))} className={selectCls}>
          {Array.from({ length: 5 }, (_, i) => now.getFullYear() - i).map(y => (
            <option key={y} value={y}>{y}</option>
          ))}
        </select>
        <Button variant="secondary" size="sm" onClick={() => refetch()}>
          Actualizar
        </Button>
      </div>

      {isLoading ? (
        <div className="space-y-4">
          {[...Array(2)].map((_, i) => (
            <div key={i} className="border border-tc-border bg-white overflow-hidden">
              <div className="h-9 animate-pulse bg-tc-stub-bg border-b border-tc-border" />
              {[...Array(4)].map((_, j) => (
                <div key={j} className="h-10 animate-pulse border-b border-tc-border bg-tc-surface last:border-0" />
              ))}
            </div>
          ))}
        </div>
      ) : data ? (
        <div className="space-y-6">
          {/* Ventas */}
          <div className="border border-tc-border bg-white overflow-hidden">
            <div className="bg-tc-stub-bg border-b border-tc-border px-4 py-2 flex items-center gap-4">
              <span className="text-[10px] font-bold tracking-[0.14em] uppercase text-tc-text-muted">Ventas</span>
              <span className="text-xs text-tc-text-secondary">
                {data.sales.count} comprobantes ·{' '}
                <span className="font-bold text-tc-purple tabular-nums">{fmtARS(data.sales.totalAmount)}</span>
              </span>
            </div>
            {data.sales.entries.length === 0 ? (
              <p className="px-4 py-6 text-sm text-tc-text-muted">Sin comprobantes en el período.</p>
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full text-sm">
                  <thead>
                    <tr className="border-b border-tc-border">
                      <th className="px-3 py-2 text-left text-[10px] font-bold tracking-[0.14em] uppercase text-tc-text-muted">Fecha</th>
                      <th className="px-3 py-2 text-left text-[10px] font-bold tracking-[0.14em] uppercase text-tc-text-muted">PV</th>
                      <th className="px-3 py-2 text-left text-[10px] font-bold tracking-[0.14em] uppercase text-tc-text-muted">Nº</th>
                      <th className="px-3 py-2 text-left text-[10px] font-bold tracking-[0.14em] uppercase text-tc-text-muted">Tipo</th>
                      <th className="px-3 py-2 text-left text-[10px] font-bold tracking-[0.14em] uppercase text-tc-text-muted">CAE</th>
                      <th className="px-3 py-2 text-left text-[10px] font-bold tracking-[0.14em] uppercase text-tc-text-muted">Cliente</th>
                      <th className="px-3 py-2 text-right text-[10px] font-bold tracking-[0.14em] uppercase text-tc-text-muted">Neto</th>
                      <th className="px-3 py-2 text-right text-[10px] font-bold tracking-[0.14em] uppercase text-tc-text-muted">IVA</th>
                      <th className="px-3 py-2 text-right text-[10px] font-bold tracking-[0.14em] uppercase text-tc-text-muted">Total</th>
                    </tr>
                  </thead>
                  <tbody>
                    {data.sales.entries.map((entry, i) => (
                      <tr key={i} className="border-b border-tc-border last:border-0 hover:bg-tc-surface">
                        <td className="px-3 py-2 font-mono text-xs tabular-nums">{entry.invoiceDate.slice(0, 10)}</td>
                        <td className="px-3 py-2 font-mono text-xs tabular-nums">{String(entry.pointOfSaleNumber).padStart(5, '0')}</td>
                        <td className="px-3 py-2 font-mono text-xs tabular-nums">{String(entry.invoiceNumber).padStart(8, '0')}</td>
                        <td className="px-3 py-2 uppercase text-xs text-tc-text-secondary">{entry.invoiceType}</td>
                        <td className="px-3 py-2 font-mono text-xs text-tc-text-secondary">{entry.cae}</td>
                        <td className="px-3 py-2 text-tc-text-secondary">{entry.customerName}</td>
                        <td className="px-3 py-2 text-right tabular-nums text-tc-text-secondary">{fmtARS(entry.netAmount)}</td>
                        <td className="px-3 py-2 text-right tabular-nums text-tc-text-secondary">{fmtARS(entry.vatAmount)}</td>
                        <td className="px-3 py-2 text-right tabular-nums font-bold text-tc-purple">{fmtARS(entry.totalAmount)}</td>
                      </tr>
                    ))}
                  </tbody>
                  <tfoot>
                    <tr className="border-t border-tc-border bg-tc-stub-bg">
                      <td colSpan={6} className="px-3 py-2 text-right text-[10px] font-bold tracking-wider uppercase text-tc-text-muted">Totales</td>
                      <td className="px-3 py-2 text-right tabular-nums font-bold text-tc-text-secondary">{fmtARS(data.sales.totalNet)}</td>
                      <td className="px-3 py-2 text-right tabular-nums font-bold text-tc-text-secondary">{fmtARS(data.sales.totalVat)}</td>
                      <td className="px-3 py-2 text-right tabular-nums font-bold text-tc-purple">{fmtARS(data.sales.totalAmount)}</td>
                    </tr>
                  </tfoot>
                </table>
              </div>
            )}
          </div>

          {/* Compras */}
          <div className="border border-tc-border bg-white overflow-hidden">
            <div className="bg-tc-stub-bg border-b border-tc-border px-4 py-2 flex items-center gap-4">
              <span className="text-[10px] font-bold tracking-[0.14em] uppercase text-tc-text-muted">Compras</span>
              <span className="text-xs text-tc-text-secondary">
                {data.purchases.count} comprobantes ·{' '}
                <span className="font-bold text-tc-purple tabular-nums">{fmtARS(data.purchases.totalAmount)}</span>
              </span>
            </div>
            {data.purchases.entries.length === 0 ? (
              <p className="px-4 py-6 text-sm text-tc-text-muted">Sin compras en el período.</p>
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full text-sm">
                  <thead>
                    <tr className="border-b border-tc-border">
                      <th className="px-3 py-2 text-left text-[10px] font-bold tracking-[0.14em] uppercase text-tc-text-muted">Fecha</th>
                      <th className="px-3 py-2 text-left text-[10px] font-bold tracking-[0.14em] uppercase text-tc-text-muted">Proveedor</th>
                      <th className="px-3 py-2 text-left text-[10px] font-bold tracking-[0.14em] uppercase text-tc-text-muted">PV</th>
                      <th className="px-3 py-2 text-left text-[10px] font-bold tracking-[0.14em] uppercase text-tc-text-muted">Nº</th>
                      <th className="px-3 py-2 text-right text-[10px] font-bold tracking-[0.14em] uppercase text-tc-text-muted">Neto</th>
                      <th className="px-3 py-2 text-right text-[10px] font-bold tracking-[0.14em] uppercase text-tc-text-muted">IVA</th>
                      <th className="px-3 py-2 text-right text-[10px] font-bold tracking-[0.14em] uppercase text-tc-text-muted">Total</th>
                    </tr>
                  </thead>
                  <tbody>
                    {data.purchases.entries.map((entry, i) => (
                      <tr key={i} className="border-b border-tc-border last:border-0 hover:bg-tc-surface">
                        <td className="px-3 py-2 font-mono text-xs tabular-nums">{entry.invoiceDate.slice(0, 10)}</td>
                        <td className="px-3 py-2 text-tc-text-secondary">{entry.customerName}</td>
                        <td className="px-3 py-2 font-mono text-xs tabular-nums">{String(entry.pointOfSaleNumber).padStart(5, '0')}</td>
                        <td className="px-3 py-2 font-mono text-xs tabular-nums">{String(entry.invoiceNumber).padStart(8, '0')}</td>
                        <td className="px-3 py-2 text-right tabular-nums text-tc-text-secondary">{fmtARS(entry.netAmount)}</td>
                        <td className="px-3 py-2 text-right tabular-nums text-tc-text-secondary">{fmtARS(entry.vatAmount)}</td>
                        <td className="px-3 py-2 text-right tabular-nums font-bold text-tc-purple">{fmtARS(entry.totalAmount)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        </div>
      ) : null}
    </div>
  )
}
