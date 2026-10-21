import { useState } from 'react'
import { Link } from 'react-router'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import {
  emitInvoiceFromCounterSale,
  emitInvoiceFromWorkOrder,
  listCertificates,
  listPointsOfSale,
  searchInvoices,
  type InvoiceDto,
  type InvoiceSourceType,
} from '../../../lib/api/invoicing'
import { ApiError } from '../../../lib/api/client'
import { Button } from '../../../components/ui/Button'
import { Modal } from '../../../components/ui/Modal'
import { FormSelect } from '../../../components/ui/FormField'
import { StatusBadge } from '../../../components/ui/StatusBadge'
import { useCan } from '../../../lib/auth/permissions'
import { INVOICE_STATUS_LABELS } from './invoiceLabels'

const fmtARS = (n: number) =>
  new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS', minimumFractionDigits: 2 }).format(n)

const CONCEPTS = [
  { value: 1, label: 'Productos' },
  { value: 2, label: 'Servicios' },
  { value: 3, label: 'Productos y servicios' },
]

interface Props {
  sourceType: InvoiceSourceType
  sourceId: string
  /** ¿El documento ya puede facturarse? (p. ej. una orden no cancelada, una venta confirmada). */
  eligible: boolean
  /** Por qué no se puede facturar todavía (se muestra en lugar del botón). */
  ineligibleReason?: string
}

/** Facturas electrónicas de un documento (orden o venta de mostrador) y el botón para emitirlas. */
export function InvoiceSection({ sourceType, sourceId, eligible, ineligibleReason }: Props) {
  const can = useCan()
  const canRead = can('invoicing.read')
  const canEmit = can('invoicing.emit')
  const queryClient = useQueryClient()
  const [emitting, setEmitting] = useState(false)

  const { data: invoices } = useQuery({
    queryKey: ['invoices', 'source', sourceId],
    queryFn: () => searchInvoices({ sourceType, sourceId, pageSize: 50 }),
    enabled: canRead,
  })
  const { data: certs } = useQuery({ queryKey: ['invoicing', 'certificates'], queryFn: listCertificates, enabled: canEmit })
  const { data: points } = useQuery({ queryKey: ['invoicing', 'points'], queryFn: listPointsOfSale, enabled: canEmit })

  if (!canRead) return null

  const items = invoices?.items ?? []
  const hasAuthorized = items.some(i => i.status === 'authorized')
  const hasCert = (certs ?? []).some(c => c.isActive)
  const activePoints = (points ?? []).filter(p => p.isActive)

  let action: React.ReactNode = null
  if (canEmit && !hasAuthorized) {
    if (!eligible) {
      action = ineligibleReason ? <p className="text-sm text-tc-text-muted">{ineligibleReason}</p> : null
    } else if (!hasCert || activePoints.length === 0) {
      action = (
        <p className="text-sm text-tc-text-secondary">
          Para facturar primero hay que configurar ARCA (certificado y punto de venta).{' '}
          <Link to="/app/invoicing/setup" className="font-semibold text-tc-purple underline">Configurar ARCA</Link>
        </p>
      )
    } else {
      action = <Button size="sm" onClick={() => setEmitting(true)}>Facturar</Button>
    }
  }

  return (
    <>
      {items.length > 0 ? (
        <ul className="mb-3 divide-y divide-tc-border border border-tc-border">
          {items.map(i => (
            <li key={i.id}>
              <Link
                to={`/app/invoicing/invoices/${i.id}`}
                className="flex flex-wrap items-center gap-x-4 gap-y-1 px-3 py-2 text-sm hover:bg-tc-surface"
              >
                <span className="font-mono text-tc-text">
                  Fcta. {i.invoiceType.toUpperCase()} {i.number ?? '—'}
                  {i.isCredit && <span className="ml-2 text-[10px] font-bold text-amber-700">NC</span>}
                </span>
                <StatusBadge status={i.status} label={INVOICE_STATUS_LABELS[i.status]} />
                <span className="ml-auto tabular-nums font-semibold text-tc-purple">{fmtARS(i.totalAmount)}</span>
              </Link>
            </li>
          ))}
        </ul>
      ) : (
        invoices && <p className="mb-3 text-sm text-tc-text-muted">Todavía no se emitió ninguna factura.</p>
      )}
      {action}

      {emitting && (
        <EmitInvoiceModal
          sourceType={sourceType}
          sourceId={sourceId}
          points={activePoints}
          onClose={() => setEmitting(false)}
          onDone={() => queryClient.invalidateQueries({ queryKey: ['invoices'] })}
        />
      )}
    </>
  )
}

function EmitInvoiceModal({
  sourceType,
  sourceId,
  points,
  onClose,
  onDone,
}: {
  sourceType: InvoiceSourceType
  sourceId: string
  points: { id: string; number: number; name: string }[]
  onClose: () => void
  onDone: () => void
}) {
  const [pointId, setPointId] = useState(points[0]?.id ?? '')
  const [concept, setConcept] = useState(sourceType === 'work_order' ? 2 : 1)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [result, setResult] = useState<InvoiceDto | null>(null)

  async function submit(e: React.FormEvent) {
    e.preventDefault()
    setBusy(true)
    setError(null)
    try {
      const req = { fiscalPointOfSaleId: pointId, concept }
      const dto = sourceType === 'work_order'
        ? await emitInvoiceFromWorkOrder(sourceId, req)
        : await emitInvoiceFromCounterSale(sourceId, req)
      setResult(dto)
      onDone()
    } catch (err) {
      setError(err instanceof ApiError ? err.detail : 'No se pudo emitir la factura. Probá de nuevo.')
    } finally {
      setBusy(false)
    }
  }

  if (result) {
    const ok = result.status === 'authorized'
    return (
      <Modal title={ok ? 'Factura emitida' : 'Factura rechazada'} onClose={onClose} className="max-w-md">
        <div className="space-y-4">
          {ok ? (
            <>
              <p className="text-sm text-tc-text-secondary">
                ARCA autorizó la <b>Factura {result.invoiceType.toUpperCase()}</b>
                {result.number != null ? <> número <b className="font-mono">{result.number}</b></> : null}.
              </p>
              {result.cae && <p className="text-xs text-tc-text-muted">CAE <span className="font-mono">{result.cae}</span></p>}
            </>
          ) : (
            <p role="alert" className="border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
              ARCA rechazó el comprobante: {result.rejectionReason ?? 'sin detalle'}. Podés corregir el dato y volver a intentar.
            </p>
          )}
          <div className="flex justify-end gap-2">
            <Link
              to={`/app/invoicing/invoices/${result.id}`}
              className="inline-flex items-center border border-tc-border px-3 py-1.5 text-xs font-bold tracking-wider uppercase text-tc-text-secondary hover:bg-tc-surface"
            >
              Ver factura
            </Link>
            <Button size="sm" onClick={onClose}>Cerrar</Button>
          </div>
        </div>
      </Modal>
    )
  }

  return (
    <Modal title="Emitir factura electrónica" onClose={onClose} className="max-w-md">
      <form onSubmit={submit} className="space-y-4">
        <p className="text-sm text-tc-text-secondary">
          Se emite por el total del documento. El tipo (A o B) se elige solo según la condición de IVA del cliente.
        </p>
        {error && <p role="alert" className="border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{error}</p>}
        <FormSelect label="Punto de venta" requiredMark value={pointId} onChange={e => setPointId(e.target.value)}>
          {points.map(p => (
            <option key={p.id} value={p.id}>{String(p.number).padStart(4, '0')} · {p.name}</option>
          ))}
        </FormSelect>
        <FormSelect label="Concepto" requiredMark value={concept} onChange={e => setConcept(Number(e.target.value))}>
          {CONCEPTS.map(c => (
            <option key={c.value} value={c.value}>{c.label}</option>
          ))}
        </FormSelect>
        <div className="flex justify-end gap-2">
          <Button type="button" variant="secondary" size="sm" onClick={onClose}>Cancelar</Button>
          <Button type="submit" size="sm" loading={busy} disabled={!pointId}>Emitir factura</Button>
        </div>
      </form>
    </Modal>
  )
}
