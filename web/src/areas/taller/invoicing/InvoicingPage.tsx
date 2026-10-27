import { formatDate } from '../../../lib/dates'
import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { searchInvoices, listCertificates, daysToExpire, EXPIRY_WARNING_DAYS, type InvoiceListDto, type InvoiceStatus, type InvoiceSourceType } from '../../../lib/api/invoicing'
import { StatusBadge } from '../../../components/ui/StatusBadge'
import { DataTable, sortableWhen, type DataTableColumn } from '../../../components/ui/DataTable'
import { Pagination } from '../../../components/ui/Pagination'
import { NavLink } from 'react-router'
import { InvoicingTabs } from './InvoicingTabs'
import { INVOICE_STATUS_LABELS } from './invoiceLabels'

const fmtARS = (n: number) =>
  new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS', minimumFractionDigits: 2 }).format(n)

const STATUS_LABELS = INVOICE_STATUS_LABELS

const SOURCE_LABELS: Record<InvoiceSourceType, string> = {
  work_order: 'Orden de Trabajo',
  counter_sale: 'Venta Mostrador',
}

const COLUMNS: DataTableColumn<InvoiceListDto>[] = [
  {
    key: 'number',
    header: 'Nº',
    widthClass: 'w-48',
    sortable: true,
    sortValue: i => i.number ?? '',
    cell: i => (
      <span className="font-mono text-tc-text">
        {i.number ?? '—'}
        {i.isCredit && (
          <span className="ml-2 rounded-md border border-amber-300 px-1 py-0.5 text-[11px] font-bold tracking-wider uppercase text-amber-700">NC</span>
        )}
      </span>
    ),
  },
  { key: 'type', header: 'Tipo', widthClass: 'w-28', cell: i => <span className="font-mono text-xs uppercase text-tc-text-secondary">Fcta. {i.invoiceType}</span> },
  { key: 'source', header: 'Origen', cell: i => <span className="text-xs text-tc-text-secondary">{SOURCE_LABELS[i.sourceType]}</span> },
  {
    key: 'status',
    header: 'Estado',
    align: 'center',
    widthClass: 'w-40',
    sortable: true,
    sortValue: i => i.status,
    cell: i => (
      <>
        <StatusBadge status={i.status} label={STATUS_LABELS[i.status]} />
        {i.isHomologation && <span className="ml-2 border border-amber-300 px-1 text-[11px] font-bold tracking-wider uppercase text-amber-700" title="Emitida en homologación: es de prueba y no tiene validez fiscal">Prueba</span>}
      </>
    ),
  },
  {
    key: 'total',
    header: 'Total',
    align: 'right',
    widthClass: 'w-36',
    sortable: true,
    sortValue: i => i.totalAmount,
    cell: i => <span className="font-bold tabular-nums text-tc-purple">{fmtARS(i.totalAmount)}</span>,
  },
  {
    key: 'date',
    header: 'Fecha',
    widthClass: 'w-32',
    sortable: true,
    sortValue: i => i.issuedAt,
    cell: i => <span className="tabular-nums text-tc-text-secondary">{formatDate(i.issuedAt)}</span>,
  },
]

export function InvoicingPage() {
  const [status, setStatus] = useState<InvoiceStatus | ''>('')
  const [page, setPage] = useState(1)

  const { data, isLoading } = useQuery({
    queryKey: ['invoices', status, page],
    queryFn: () => searchInvoices({ status: status || undefined, page, pageSize: 10 }),
  })

  const { data: certs } = useQuery({ queryKey: ['fiscal-certificates'], queryFn: listCertificates })
  const expiring = (certs ?? [])
    .filter(c => c.status === 'active')
    .map(c => ({ cert: c, days: daysToExpire(c) }))
    .filter(x => x.days !== null && x.days <= EXPIRY_WARNING_DAYS)
    .sort((a, b) => (a.days ?? 0) - (b.days ?? 0))[0]

  return (
    <div className="space-y-4">
      <h1 className="text-lg font-bold tracking-wide text-tc-text uppercase">Facturación</h1>

      <InvoicingTabs />

      {expiring && (
        <p role="alert" className="border border-amber-300 bg-amber-50 px-3 py-2 text-sm text-amber-800">
          {(expiring.days ?? 0) < 0
            ? `El certificado «${expiring.cert.name}» está vencido: no se puede facturar.`
            : `El certificado «${expiring.cert.name}» vence en ${expiring.days} día${expiring.days === 1 ? '' : 's'}.`}{' '}
          <NavLink to="/app/invoicing/setup" className="font-semibold underline">Ir a Configuración ARCA</NavLink>
        </p>
      )}

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
        <div className="border border-dashed border-tc-border px-6 py-12 text-center">
          <p className="mb-1 text-sm font-medium text-tc-text-secondary">No hay facturas todavía</p>
          <p className="mb-4 text-xs text-tc-text-muted">Para emitir facturas electrónicas, primero configurá el certificado ARCA.</p>
          <NavLink
            to="/app/invoicing/setup"
            className="inline-flex items-center gap-1.5 border border-tc-purple bg-tc-purple px-4 py-2 text-xs font-bold tracking-wider uppercase text-white hover:bg-tc-purple-mid transition-colors"
          >
            Configurar ARCA
          </NavLink>
        </div>
      ) : (
        <>
          <DataTable<InvoiceListDto>
            label="Facturas"
            columns={sortableWhen(COLUMNS, data.totalCount <= 10)}
            rows={data.items}
            getRowId={i => i.id}
            rowHref={i => `/app/invoicing/invoices/${i.id}`}
            rowLabel={i => `Ver factura ${i.number ?? ""}`}
            cardStub={i => <span className="px-1 text-center font-mono text-[11px] font-bold text-tc-text-secondary">{i.number ?? '—'}</span>}
            renderCard={i => (
              <>
                <p className="text-sm text-tc-text-secondary">
                  Fcta. {i.invoiceType.toUpperCase()} · {SOURCE_LABELS[i.sourceType]}
                  {i.isCredit && <span className="ml-2 text-[11px] font-bold text-amber-700">NC</span>}
                  {i.isHomologation && <span className="ml-2 border border-amber-300 px-1 text-[11px] font-bold tracking-wider uppercase text-amber-700" title="Emitida en homologación: es de prueba y no tiene validez fiscal">Prueba</span>}
                </p>
                <p className="text-xs tabular-nums text-tc-text-muted">{formatDate(i.issuedAt)}</p>
                <div className="mt-1.5 flex flex-wrap items-center gap-x-3 gap-y-1">
                  <StatusBadge status={i.status} label={STATUS_LABELS[i.status]} />
                  <span className="text-sm font-bold tabular-nums text-tc-purple">{fmtARS(i.totalAmount)}</span>
                </div>
              </>
            )}
          />
          <Pagination page={page} pageSize={10} total={data.totalCount} noun="facturas" onPage={setPage} />
        </>
      )}
    </div>
  )
}
