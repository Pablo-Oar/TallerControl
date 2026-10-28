import { formatDateTime } from '../../../lib/dates'
import { useState } from 'react'
import { useNavigate } from 'react-router'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import {
  appointmentsApi,
  getAppointmentStatusLabel,
  type AppointmentDto,
  type AppointmentListDto,
  type AppointmentStatus,
} from '../../../lib/api/appointments'
import { useCan } from '../../../lib/auth/permissions'
import { ShareLink } from '../../../components/ShareLink'
import { AppointmentsTabs } from './AppointmentsTabs'
import { ConvertAppointmentModal } from './ConvertAppointmentModal'
import { Button } from '../../../components/ui/Button'
import { Modal } from '../../../components/ui/Modal'
import { StatusBadge } from '../../../components/ui/StatusBadge'
import { DataTable, RowAction, sortableWhen, type DataTableColumn } from '../../../components/ui/DataTable'
import { Pagination } from '../../../components/ui/Pagination'
import { AppointmentFormModal } from './AppointmentFormModal'
import { inputCls } from '../../../lib/styles'

const STATUS_OPTIONS: { value: AppointmentStatus | ''; label: string }[] = [
  { value: '', label: 'Todos los estados' },
  { value: 'pending', label: 'Pendientes' },
  { value: 'confirmed', label: 'Confirmados' },
  { value: 'completed', label: 'Completados' },
  { value: 'cancelled', label: 'Cancelados' },
]

const fmtWhen = (iso: string) =>
  formatDateTime(iso)
const fmtTime = (iso: string) => new Date(iso).toLocaleTimeString('es-AR', { hour: '2-digit', minute: '2-digit', hour12: false })
const fmtDay = (iso: string) =>
  new Date(iso).toLocaleDateString('es-AR', { day: '2-digit', month: 'short' }).replace('.', '').toUpperCase()
const actionCls = 'max-md:ml-0 max-md:flex-1 max-md:basis-[45%]'
const fieldLabelCls = 'mb-1 block text-xs font-bold tracking-wider uppercase text-tc-text-secondary'
const ctrlCls = `${inputCls} min-h-11 sm:min-h-0`

const isOpen = (a: AppointmentListDto) => a.status === 'pending' || a.status === 'confirmed'

const COLUMNS: DataTableColumn<AppointmentListDto>[] = [
  {
    key: 'when',
    header: 'Fecha / hora',
    widthClass: 'w-44',
    sortable: true,
    sortValue: a => a.scheduledAt,
    cell: a => <span className="whitespace-nowrap font-medium tabular-nums text-tc-text">{fmtWhen(a.scheduledAt)}</span>,
  },
  { key: 'customer', header: 'Cliente', sortable: true, sortValue: a => a.customerName, cell: a => <span className="text-tc-text-secondary">{a.customerName}</span> },
  { key: 'plate', header: 'Vehículo', widthClass: 'w-32', cell: a => <span className="font-mono tracking-widest text-tc-text-secondary">{a.vehiclePlate}</span> },
  { key: 'mechanic', header: 'Mecánico', widthClass: 'w-40', cell: a => <span className="text-tc-text-secondary">{a.mechanicName ?? <span className="text-tc-text-muted">—</span>}</span> },
  { key: 'notes', header: 'Notas', cell: a => <span className="line-clamp-1 text-xs text-tc-text-secondary">{a.notes ?? <span className="text-tc-text-muted">—</span>}</span> },
  { key: 'duration', header: 'Duración', widthClass: 'w-28', cell: a => <span className="tabular-nums text-tc-text-secondary">{a.totalDurationMinutes} min</span> },
  {
    key: 'status',
    header: 'Estado',
    align: 'center',
    widthClass: 'w-40',
    sortable: true,
    sortValue: a => a.status,
    cell: a => <StatusBadge status={a.status} label={getAppointmentStatusLabel(a.status)} />,
  },
]

export function AppointmentsPage() {
  const qc = useQueryClient()
  const navigate = useNavigate()
  const can = useCan()
  const [page, setPage] = useState(1)
  const [status, setStatus] = useState<AppointmentStatus | ''>('')
  const [date, setDate] = useState('')
  const [showModal, setShowModal] = useState(false)
  const [editing, setEditing] = useState<AppointmentDto | null>(null)
  const [converting, setConverting] = useState<AppointmentListDto | null>(null)
  const [convertError, setConvertError] = useState<string | null>(null)
  const [shareLink, setShareLink] = useState<{ url: string; created: boolean } | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [cancellingId, setCancellingId] = useState<string | null>(null)
  const [cancelReason, setCancelReason] = useState('')

  const { data, isLoading } = useQuery({
    queryKey: ['appointments', page, status, date],
    queryFn: () =>
      appointmentsApi.search({
        page,
        pageSize: 10,
        status: status || undefined,
        date: date || undefined,
      }),
  })

  const invalidate = () => qc.invalidateQueries({ queryKey: ['appointments'] })

  const cancelMut = useMutation({
    mutationFn: ({ id, reason }: { id: string; reason: string }) =>
      appointmentsApi.cancel(id, reason),
    onSuccess: () => {
      invalidate()
      setCancellingId(null)
      setCancelReason('')
    },
    onError: (e: Error) => setError(e.message),
  })

  const convertMut = useMutation({
    mutationFn: ({ id, odometerKm }: { id: string; odometerKm: number }) =>
      appointmentsApi.convertToWorkOrder(id, { odometerKm }),
    onSuccess: order => {
      invalidate()
      setConverting(null)
      navigate(`/app/work-orders/${order.id}`)
    },
    onError: (e: Error) => setConvertError(e.message),
  })

  async function openEdit(id: string) {
    try {
      setEditing(await appointmentsApi.get(id))
    } catch (e) {
      setError((e as Error).message)
    }
  }

  async function openLink(id: string) {
    try {
      setShareLink({ url: await appointmentsApi.publicLink(id), created: false })
    } catch (e) {
      setError((e as Error).message)
    }
  }

  const items: AppointmentListDto[] = data?.items ?? []
  const total = data?.total ?? 0

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-bold tracking-widest text-tc-text uppercase">Agenda</h1>
        <Button size="sm" onClick={() => setShowModal(true)}>Nuevo turno</Button>
      </div>

      <AppointmentsTabs />

      {error && (
        <div className="border border-red-200 bg-red-50 px-4 py-2 text-sm text-red-700 flex items-center justify-between">
          <span>{error}</span>
          <button className="text-xs underline" onClick={() => setError(null)}>Cerrar</button>
        </div>
      )}

      <div className="flex flex-wrap gap-3">
        <div>
          <label htmlFor="ap-date" className={fieldLabelCls}>Fecha</label>
          <input
            id="ap-date"
            type="date"
            value={date}
            onChange={e => { setDate(e.target.value); setPage(1) }}
            className={ctrlCls}
          />
        </div>
        <div>
          <label htmlFor="ap-status" className={fieldLabelCls}>Estado</label>
          <select
            id="ap-status"
            value={status}
            onChange={e => { setStatus(e.target.value as AppointmentStatus | ''); setPage(1) }}
            className={ctrlCls}
          >
            {STATUS_OPTIONS.map(o => (
              <option key={o.value} value={o.value}>{o.label}</option>
            ))}
          </select>
        </div>
      </div>

      <DataTable<AppointmentListDto>
        label="Agenda"
        columns={sortableWhen(COLUMNS, total <= 10)}
        rows={items}
        getRowId={a => a.id}
        actions={a =>
          isOpen(a) ? (
            <>
              {can('workorders.write') && (
                <RowAction tone="purple" className={actionCls} onClick={() => { setConvertError(null); setConverting(a) }}>
                  Crear orden
                </RowAction>
              )}
              <RowAction tone="purple" className={actionCls} onClick={() => openEdit(a.id)}>Editar</RowAction>
              <RowAction tone="purple" className={actionCls} onClick={() => openLink(a.id)}>Enlace</RowAction>
              <RowAction tone="danger" className={actionCls} onClick={() => { setCancellingId(a.id); setCancelReason('') }}>
                Cancelar
              </RowAction>
            </>
          ) : null
        }
        cardStub={a => <span className="text-center text-[11px] font-bold leading-tight tabular-nums text-tc-text-secondary">{fmtDay(a.scheduledAt)}</span>}
        rowClassName={a => (a.status === 'cancelled' ? 'opacity-60' : undefined)}
        renderCard={a => (
          <>
            <p className="text-sm font-medium tabular-nums text-tc-text">{fmtTime(a.scheduledAt)}</p>
            <p className="truncate text-sm text-tc-text-secondary">{a.customerName}</p>
            <p className="font-mono text-xs tracking-widest text-tc-text-muted">{a.vehiclePlate} · {a.totalDurationMinutes} min</p>
            <div className="mt-1.5"><StatusBadge status={a.status} label={getAppointmentStatusLabel(a.status)} /></div>
          </>
        )}
        loading={isLoading}
        emptyMessage="No hay turnos para los filtros seleccionados."
      />

      <Pagination page={page} pageSize={10} total={total} noun="turnos" onPage={setPage} />

      {showModal && (
        <AppointmentFormModal
          onClose={() => { setShowModal(false); invalidate() }}
          onCreated={url => { setShowModal(false); invalidate(); setShareLink({ url, created: true }) }}
        />
      )}

      {editing && (
        <AppointmentFormModal editData={editing} onClose={() => { setEditing(null); invalidate() }} />
      )}

      {converting && (
        <ConvertAppointmentModal
          vehicleId={converting.vehicleId}
          vehiclePlate={converting.vehiclePlate}
          busy={convertMut.isPending}
          error={convertError}
          onConfirm={odometerKm => convertMut.mutate({ id: converting.id, odometerKm })}
          onClose={() => setConverting(null)}
        />
      )}

      {shareLink && (
        <Modal title={shareLink.created ? 'Turno creado' : 'Enlace del turno'} onClose={() => setShareLink(null)} className="max-w-md">
          <div className="space-y-3">
            <p className="text-sm text-tc-text-secondary">
              Con este enlace el cliente ve su turno y lo confirma (o avisa que no puede ir).
              {shareLink.created
                ? ' Si el cliente tiene email, ya se le envió.'
                : ' Este enlace reemplaza al anterior: el que se mandó antes deja de servir.'}
            </p>
            <ShareLink label="Enlace del turno" url={shareLink.url} whatsappText="Confirmá tu turno en el taller:" />
            <div className="flex justify-end">
              <Button size="sm" onClick={() => setShareLink(null)}>Listo</Button>
            </div>
          </div>
        </Modal>
      )}

      {cancellingId && (
        <Modal title="Cancelar turno" onClose={() => setCancellingId(null)} className="max-w-sm">
          <div className="space-y-4">
            <textarea
              value={cancelReason}
              onChange={e => setCancelReason(e.target.value)}
              placeholder="Motivo de cancelación (obligatorio)"
              rows={3}
              className="w-full border border-tc-border bg-white px-3 py-2 text-sm outline-none focus:border-tc-border-mid transition-colors"
            />
            <div className="flex justify-end gap-2">
              <Button type="button" variant="secondary" size="sm" onClick={() => setCancellingId(null)}>
                Volver
              </Button>
              <Button
                variant="danger"
                size="sm"
                disabled={!cancelReason.trim() || cancelMut.isPending}
                loading={cancelMut.isPending}
                onClick={() => cancelMut.mutate({ id: cancellingId, reason: cancelReason })}
              >
                Confirmar cancelación
              </Button>
            </div>
          </div>
        </Modal>
      )}
    </div>
  )
}
