import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { getIvaBook, downloadCitiExport } from '../../../lib/api/invoicing'
import { Button } from '../../../components/ui/Button'

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

  const MONTHS = [
    'Enero', 'Febrero', 'Marzo', 'Abril', 'Mayo', 'Junio',
    'Julio', 'Agosto', 'Septiembre', 'Octubre', 'Noviembre', 'Diciembre',
  ]

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold">Libro IVA Digital</h1>
        <Button variant="secondary" loading={downloading} onClick={handleDownloadCiti}>
          Descargar CITI (.zip)
        </Button>
      </div>

      <div className="flex gap-3">
        <select
          value={month}
          onChange={e => setMonth(Number(e.target.value))}
          className="rounded border border-slate-300 px-3 py-1.5 text-sm"
        >
          {MONTHS.map((m, i) => (
            <option key={i + 1} value={i + 1}>{m}</option>
          ))}
        </select>
        <select
          value={year}
          onChange={e => setYear(Number(e.target.value))}
          className="rounded border border-slate-300 px-3 py-1.5 text-sm"
        >
          {Array.from({ length: 5 }, (_, i) => now.getFullYear() - i).map(y => (
            <option key={y} value={y}>{y}</option>
          ))}
        </select>
        <Button variant="secondary" onClick={() => refetch()}>
          Actualizar
        </Button>
      </div>

      {isLoading ? (
        <p className="text-sm text-slate-500">Cargando...</p>
      ) : data ? (
        <div className="space-y-6">
          {/* Ventas */}
          <section>
            <div className="mb-2 flex items-center gap-4">
              <h2 className="text-base font-semibold">Ventas</h2>
              <span className="text-sm text-slate-500">
                {data.sales.count} comprobantes · $ {data.sales.totalAmount.toFixed(2)}
              </span>
            </div>
            {data.sales.entries.length === 0 ? (
              <p className="text-sm text-slate-500">Sin comprobantes en el período.</p>
            ) : (
              <div className="overflow-x-auto rounded border border-slate-200">
                <table className="min-w-full text-sm">
                  <thead className="bg-slate-50 text-left text-xs font-medium uppercase text-slate-500">
                    <tr>
                      <th className="px-3 py-2">Fecha</th>
                      <th className="px-3 py-2">PV</th>
                      <th className="px-3 py-2">Nº</th>
                      <th className="px-3 py-2">Tipo</th>
                      <th className="px-3 py-2">CAE</th>
                      <th className="px-3 py-2">Cliente</th>
                      <th className="px-3 py-2 text-right">Neto</th>
                      <th className="px-3 py-2 text-right">IVA</th>
                      <th className="px-3 py-2 text-right">Total</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-100">
                    {data.sales.entries.map((entry, i) => (
                      <tr key={i} className="hover:bg-slate-50">
                        <td className="px-3 py-2 font-mono text-xs">
                          {entry.invoiceDate.slice(0, 10)}
                        </td>
                        <td className="px-3 py-2 font-mono text-xs">
                          {String(entry.pointOfSaleNumber).padStart(5, '0')}
                        </td>
                        <td className="px-3 py-2 font-mono text-xs">
                          {String(entry.invoiceNumber).padStart(8, '0')}
                        </td>
                        <td className="px-3 py-2 uppercase">{entry.invoiceType}</td>
                        <td className="px-3 py-2 font-mono text-xs">{entry.cae}</td>
                        <td className="px-3 py-2">{entry.customerName}</td>
                        <td className="px-3 py-2 text-right">$ {entry.netAmount.toFixed(2)}</td>
                        <td className="px-3 py-2 text-right">$ {entry.vatAmount.toFixed(2)}</td>
                        <td className="px-3 py-2 text-right">$ {entry.totalAmount.toFixed(2)}</td>
                      </tr>
                    ))}
                  </tbody>
                  <tfoot className="border-t border-slate-200 bg-slate-50 font-medium">
                    <tr>
                      <td colSpan={6} className="px-3 py-2 text-right">Totales</td>
                      <td className="px-3 py-2 text-right">$ {data.sales.totalNet.toFixed(2)}</td>
                      <td className="px-3 py-2 text-right">$ {data.sales.totalVat.toFixed(2)}</td>
                      <td className="px-3 py-2 text-right">$ {data.sales.totalAmount.toFixed(2)}</td>
                    </tr>
                  </tfoot>
                </table>
              </div>
            )}
          </section>

          {/* Compras */}
          <section>
            <div className="mb-2 flex items-center gap-4">
              <h2 className="text-base font-semibold">Compras</h2>
              <span className="text-sm text-slate-500">
                {data.purchases.count} comprobantes · $ {data.purchases.totalAmount.toFixed(2)}
              </span>
            </div>
            {data.purchases.entries.length === 0 ? (
              <p className="text-sm text-slate-500">Sin compras en el período.</p>
            ) : (
              <div className="overflow-x-auto rounded border border-slate-200">
                <table className="min-w-full text-sm">
                  <thead className="bg-slate-50 text-left text-xs font-medium uppercase text-slate-500">
                    <tr>
                      <th className="px-3 py-2">Fecha</th>
                      <th className="px-3 py-2">Proveedor</th>
                      <th className="px-3 py-2">PV</th>
                      <th className="px-3 py-2">Nº</th>
                      <th className="px-3 py-2 text-right">Neto</th>
                      <th className="px-3 py-2 text-right">IVA</th>
                      <th className="px-3 py-2 text-right">Total</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-100">
                    {data.purchases.entries.map((entry, i) => (
                      <tr key={i} className="hover:bg-slate-50">
                        <td className="px-3 py-2 font-mono text-xs">
                          {entry.invoiceDate.slice(0, 10)}
                        </td>
                        <td className="px-3 py-2">{entry.customerName}</td>
                        <td className="px-3 py-2 font-mono text-xs">
                          {String(entry.pointOfSaleNumber).padStart(5, '0')}
                        </td>
                        <td className="px-3 py-2 font-mono text-xs">
                          {String(entry.invoiceNumber).padStart(8, '0')}
                        </td>
                        <td className="px-3 py-2 text-right">$ {entry.netAmount.toFixed(2)}</td>
                        <td className="px-3 py-2 text-right">$ {entry.vatAmount.toFixed(2)}</td>
                        <td className="px-3 py-2 text-right">$ {entry.totalAmount.toFixed(2)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>
        </div>
      ) : null}
    </div>
  )
}
