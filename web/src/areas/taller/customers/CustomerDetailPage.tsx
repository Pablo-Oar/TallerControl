import { useState } from 'react'
import { Link, useParams } from 'react-router'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { customersApi } from '../../../lib/api/customers'
import { workOrdersApi, STATUS_LABELS as ORDER_STATUS_LABELS } from '../../../lib/api/workOrders'
import { quotesApi, QUOTE_STATUS_LABELS } from '../../../lib/api/quotes'
import { Button } from '../../../components/ui/Button'
import { StatusBadge } from '../../../components/ui/StatusBadge'
import { useCan } from '../../../lib/auth/permissions'
import { CustomerFormModal } from './CustomerFormModal'

const fmtARS = (n: number) =>
  new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS', minimumFractionDigits: 2 }).format(n)

const fmtDate = (iso: string) => new Date(iso).toLocaleDateString('es-AR')

const VAT_LABELS: Record<string, string> = {
  consumidor_final: 'Consumidor final',
  responsable_inscripto: 'Responsable inscripto',
  monotributo: 'Monotributo',
  exento: 'Exento',
  no_responsable: 'No responsable',
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <section className="border border-tc-border bg-white">
      <div className="bg-tc-stub-bg border-b border-tc-border px-4 py-2">
        <span className="text-[11px] font-bold tracking-[0.14em] uppercase text-tc-text-muted">{title}</span>
      </div>
      <div className="p-4">{children}</div>
    </section>
  )
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div>
      <dt className="mb-1 text-[11px] font-bold uppercase tracking-wider text-tc-text-muted">{label}</dt>
      <dd className="text-tc-text">{children}</dd>
    </div>
  )
}

/** P-04: ficha del cliente — datos, saldo, vehículos y sus órdenes y presupuestos. */
export function CustomerDetailPage() {
  const { id } = useParams<{ id: string }>()
  const queryClient = useQueryClient()
  const can = useCan()
  const [editing, setEditing] = useState(false)
  const canOrders = can('workorders.read')
  const canQuotes = can('quotes.read')

  const { data: customer, isLoading, error } = useQuery({
    queryKey: ['customer', id],
    queryFn: () => customersApi.get(id!),
    enabled: !!id,
  })

  const orders = useQuery({
    queryKey: ['customer-orders', id],
    queryFn: () => workOrdersApi.search(null, null, null, null, null, null, 1, 10, null, { key: 'createdat', dir: 'desc' }, id),
    enabled: !!id && canOrders,
  })

  const quotes = useQuery({
    queryKey: ['customer-quotes', id],
    queryFn: () => quotesApi.search(null, id, null, null, null, 1, 10, { key: 'createdat', dir: 'desc' }),
    enabled: !!id && canQuotes,
  })

  if (isLoading) {
    return (
      <div className="flex justify-center py-16">
        <div className="h-6 w-6 animate-spin border-2 border-tc-purple border-t-transparent" />
      </div>
    )
  }
  if (error || !customer) {
    return <p className="border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">No se pudo cargar el cliente.</p>
  }

  return (
    <div className="space-y-5">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="text-xl font-bold text-tc-text">{customer.name}</h1>
          <p className="mt-1 text-sm text-tc-text-secondary">
            {customer.balance > 0
              ? <>Saldo por cobrar: <span className="font-bold text-tc-stamp-red tabular-nums">{fmtARS(customer.balance)}</span></>
              : 'Sin saldo pendiente'}
          </p>
        </div>
        <div className="flex gap-2">
          <Link to="/app/customers">
            <Button variant="secondary" size="sm">← Volver</Button>
          </Link>
          {can('customers.write') && <Button size="sm" onClick={() => setEditing(true)}>Editar</Button>}
        </div>
      </div>

      <Section title="Datos">
        <dl className="grid grid-cols-2 gap-4 text-sm md:grid-cols-4">
          <Field label="Teléfono">{customer.phone ?? '—'}</Field>
          <Field label="Email">{customer.email ?? '—'}</Field>
          <Field label="Documento">{customer.docNumber ? `${customer.docType?.toUpperCase() ?? ''} ${customer.docNumber}`.trim() : '—'}</Field>
          <Field label="Condición IVA">{VAT_LABELS[customer.vatCondition] ?? customer.vatCondition}</Field>
          <Field label="Dirección">{customer.address ?? '—'}</Field>
          <Field label="Avisos">{customer.doNotContact ? 'No contactar' : 'Acepta avisos'}</Field>
        </dl>
        {customer.notes && <p className="mt-4 whitespace-pre-line text-sm text-tc-text-secondary">{customer.notes}</p>}
      </Section>

      <Section title={`Vehículos (${customer.vehicles.length})`}>
        {customer.vehicles.length === 0 ? (
          <p className="text-sm text-tc-text-muted">Todavía no tiene vehículos cargados.</p>
        ) : (
          <ul className="divide-y divide-tc-border">
            {customer.vehicles.map(v => (
              <li key={v.id} className="flex items-center justify-between py-2 text-sm">
                <Link to={`/app/vehicles/${v.id}`} className="font-mono font-bold tracking-widest text-tc-purple hover:underline">
                  {v.plate}
                </Link>
                <span className="text-tc-text-secondary">{[v.brand, v.model].filter(Boolean).join(' ') || '—'}</span>
              </li>
            ))}
          </ul>
        )}
      </Section>

      {canOrders && (
        <Section title="Últimas órdenes">
          {orders.isLoading ? (
            <p className="text-sm text-tc-text-muted">Cargando…</p>
          ) : (orders.data?.items.length ?? 0) === 0 ? (
            <p className="text-sm text-tc-text-muted">Sin órdenes.</p>
          ) : (
            <ul className="divide-y divide-tc-border">
              {orders.data!.items.map(o => (
                <li key={o.id} className="flex flex-wrap items-center justify-between gap-2 py-2 text-sm">
                  <Link to={`/app/work-orders/${o.id}`} className="font-medium text-tc-purple hover:underline">
                    Orden {o.number} · <span className="font-mono">{o.vehiclePlate}</span>
                  </Link>
                  <span className="flex items-center gap-3">
                    <span className="text-tc-text-muted">{fmtDate(o.createdAt)}</span>
                    {o.total != null && <span className="tabular-nums text-tc-text">{fmtARS(o.total)}</span>}
                    <StatusBadge status={o.status} label={ORDER_STATUS_LABELS[o.status] ?? o.status} />
                  </span>
                </li>
              ))}
            </ul>
          )}
        </Section>
      )}

      {canQuotes && (
        <Section title="Últimos presupuestos">
          {quotes.isLoading ? (
            <p className="text-sm text-tc-text-muted">Cargando…</p>
          ) : (quotes.data?.items.length ?? 0) === 0 ? (
            <p className="text-sm text-tc-text-muted">Sin presupuestos.</p>
          ) : (
            <ul className="divide-y divide-tc-border">
              {quotes.data!.items.map(q => (
                <li key={q.id} className="flex flex-wrap items-center justify-between gap-2 py-2 text-sm">
                  <Link to={`/app/quotes/${q.id}`} className="font-medium text-tc-purple hover:underline">
                    Presupuesto {q.number} · <span className="font-mono">{q.vehiclePlate}</span>
                  </Link>
                  <span className="flex items-center gap-3">
                    <span className="text-tc-text-muted">{fmtDate(q.createdAt)}</span>
                    <span className="tabular-nums text-tc-text">{fmtARS(q.total)}</span>
                    <StatusBadge status={q.status} label={QUOTE_STATUS_LABELS[q.status] ?? q.status} />
                  </span>
                </li>
              ))}
            </ul>
          )}
        </Section>
      )}

      {editing && (
        <CustomerFormModal
          customer={customer}
          onClose={() => setEditing(false)}
          onSuccess={() => {
            setEditing(false)
            queryClient.invalidateQueries({ queryKey: ['customer', id] })
          }}
        />
      )}
    </div>
  )
}
