import { Link, useNavigate, useParams } from 'react-router'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { getInvoice, listPointsOfSale, voidInvoice } from '../../../lib/api/invoicing'
import { ApiError } from '../../../lib/api/client'
import { Button } from '../../../components/ui/Button'
import { StatusBadge } from '../../../components/ui/StatusBadge'
import { LoadError } from '../../../components/LoadError'
import { confirmDialog, notify } from '../../../components/ui/dialogs'
import { useCan } from '../../../lib/auth/permissions'
import { INVOICE_STATUS_LABELS } from './invoiceLabels'

const fmtARS = (n: number) =>
  new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS', minimumFractionDigits: 2 }).format(n)
const fmtDate = (iso: string) => new Date(iso).toLocaleDateString('es-AR')

export function InvoiceDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const canEmit = useCan()('invoicing.emit')

  const { data: invoice, isLoading, error, refetch } = useQuery({
    queryKey: ['invoice', id],
    queryFn: () => getInvoice(id!),
    enabled: !!id,
  })
  const { data: points } = useQuery({ queryKey: ['invoicing', 'points'], queryFn: listPointsOfSale })

  const voidMut = useMutation({
    mutationFn: () => voidInvoice(id!),
    onSuccess: credit => {
      queryClient.invalidateQueries({ queryKey: ['invoices'] })
      queryClient.invalidateQueries({ queryKey: ['invoice'] })
      navigate(`/app/invoicing/invoices/${credit.id}`)
    },
    onError: err => notify(err instanceof ApiError ? err.detail : 'No se pudo anular la factura.'),
  })

  if (isLoading) return <p className="text-sm text-tc-text-muted">Cargando factura…</p>
  if (error || !invoice) return <LoadError what="la factura" onRetry={() => refetch()} />

  const pos = points?.find(p => p.id === invoice.fiscalPointOfSaleId)
  const fullNumber =
    invoice.number != null
      ? `${pos ? String(pos.number).padStart(4, '0') + '-' : ''}${String(invoice.number).padStart(8, '0')}`
      : '—'
  const sourceHref = invoice.sourceType === 'work_order' ? `/app/work-orders/${invoice.sourceId}` : '/app/counter-sales'

  async function handleVoid() {
    const ok = await confirmDialog(
      'Se emitirá una nota de crédito por el total de esta factura. No se puede deshacer. ¿Anular la factura?',
      { danger: true, confirmLabel: 'Emitir nota de crédito' },
    )
    if (ok) voidMut.mutate()
  }

  return (
    <div className="space-y-5">
      <div className="flex flex-wrap items-center gap-3">
        <Link to="/app/invoicing" className="text-xs font-bold tracking-wider uppercase text-tc-text-secondary hover:text-tc-purple">
          ← Facturas
        </Link>
        <h1 className="text-lg font-bold tracking-wide text-tc-text uppercase">
          {invoice.isCredit ? 'Nota de crédito' : 'Factura'} {invoice.invoiceType.toUpperCase()}
        </h1>
        <StatusBadge status={invoice.status} label={INVOICE_STATUS_LABELS[invoice.status]} />
        {invoice.isHomologation && <span className="ml-2 border border-amber-300 px-1 text-[10px] font-bold tracking-wider uppercase text-amber-700" title="Emitida en homologación: es de prueba y no tiene validez fiscal">Prueba</span>}
      </div>

      {invoice.status === 'rejected' && (
        <p role="alert" className="border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          ARCA rechazó este comprobante: {invoice.rejectionReason ?? 'sin detalle'}.
        </p>
      )}

      <dl className="grid gap-x-8 gap-y-3 border border-tc-border bg-white p-4 text-sm sm:grid-cols-2">
        <Item label="Número"><span className="font-mono">{fullNumber}</span></Item>
        <Item label="Fecha">{fmtDate(invoice.issuedAt)}</Item>
        <Item label="Origen">
          <Link to={sourceHref} className="font-semibold text-tc-purple underline">
            {invoice.sourceType === 'work_order' ? 'Orden de trabajo' : 'Venta de mostrador'}
          </Link>
        </Item>
        {invoice.originalInvoiceId && (
          <Item label="Anula la factura">
            <Link to={`/app/invoicing/invoices/${invoice.originalInvoiceId}`} className="font-semibold text-tc-purple underline">
              Ver factura original
            </Link>
          </Item>
        )}
        <Item label="Neto">{fmtARS(invoice.netAmount)}</Item>
        <Item label="IVA">{fmtARS(invoice.vatAmount)}</Item>
        <Item label="Total"><span className="font-bold text-tc-purple">{fmtARS(invoice.totalAmount)}</span></Item>
        <Item label="CAE"><span className="font-mono">{invoice.cae ?? '—'}</span></Item>
        <Item label="Vencimiento del CAE">{invoice.caeExpiresAt ? fmtDate(invoice.caeExpiresAt) : '—'}</Item>
      </dl>

      {canEmit && invoice.status === 'authorized' && !invoice.isCredit && (
        <div className="flex justify-end">
          <Button variant="danger" size="sm" loading={voidMut.isPending} onClick={handleVoid}>
            Anular con nota de crédito
          </Button>
        </div>
      )}
    </div>
  )
}

function Item({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div>
      <dt className="text-[10px] font-bold tracking-[0.14em] uppercase text-tc-text-muted">{label}</dt>
      <dd className="mt-0.5 text-tc-text-secondary">{children}</dd>
    </div>
  )
}
