import { formatDate, formatDateOnly, localDateISO } from '../../../lib/dates'
import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { remindersApi, getReminderKindLabel, type ReminderListDto, type ReminderStatusFilter } from '../../../lib/api/reminders'
import { Button } from '../../../components/ui/Button'
import { DataTable, RowAction, sortableWhen, type DataTableColumn } from '../../../components/ui/DataTable'
import { Pagination } from '../../../components/ui/Pagination'
import { RowMenu } from '../../../components/ui/RowMenu'
import { Tabs } from '../../../components/ui/Tabs'
import { ReminderFormModal } from './ReminderFormModal'
import { confirmDialog, notify } from '../../../components/ui/dialogs'

const dueSoon = (r: ReminderListDto) =>
  r.dueDate ? r.dueDate.slice(0, 10) <= localDateISO(7) : false

const dash = <span className="text-tc-text-muted">—</span>

const COLUMNS: DataTableColumn<ReminderListDto>[] = [
  {
    key: 'plate',
    header: 'Vehículo',
    widthClass: 'w-32',
    sortable: true,
    sortValue: r => r.vehiclePlate,
    cell: r => <span className="font-mono tracking-widest text-tc-text">{r.vehiclePlate}</span>,
  },
  { key: 'kind', header: 'Tipo', widthClass: 'w-40', cell: r => <span className="text-xs text-tc-text-secondary">{getReminderKindLabel(r.kind)}</span> },
  {
    key: 'title',
    header: 'Título',
    cell: r => (
      <>
        {dueSoon(r) && (
          <span className="mr-1.5 inline-block rounded-md border border-amber-300 bg-amber-50 px-1.5 py-0.5 text-[11px] font-bold tracking-wider uppercase text-amber-700">
            Próximo
          </span>
        )}
        <span className="text-tc-text">{r.title}</span>
      </>
    ),
  },
  {
    key: 'due',
    header: 'Fecha límite',
    widthClass: 'w-36',
    sortable: true,
    sortValue: r => r.dueDate ?? '9999',
    cell: r => <span className="tabular-nums text-tc-text-secondary">{r.dueDate ? formatDateOnly(r.dueDate) : dash}</span>,
  },
  {
    key: 'km',
    header: 'Km límite',
    widthClass: 'w-32',
    cell: r => <span className="tabular-nums text-tc-text-secondary">{r.dueKm != null ? `${r.dueKm.toLocaleString('es-AR')} km` : dash}</span>,
  },
  {
    key: 'notified',
    header: 'Último aviso',
    widthClass: 'w-36',
    cell: r => <span className="tabular-nums text-tc-text-secondary">{r.lastNotifiedAt ? formatDate(r.lastNotifiedAt) : dash}</span>,
  },
]

export function RemindersPage() {
  const qc = useQueryClient()
  const [page, setPage] = useState(1)
  const [status, setStatus] = useState<ReminderStatusFilter>('pending')
  const [showModal, setShowModal] = useState(false)
  const [editId, setEditId] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  const { data, isLoading } = useQuery({
    queryKey: ['reminders', status, page],
    queryFn: () => remindersApi.search({ status, page, pageSize: 10 }),
  })

  const invalidate = () => qc.invalidateQueries({ queryKey: ['reminders'] })

  const deleteMut = useMutation({
    mutationFn: (id: string) => remindersApi.delete(id),
    onSuccess: invalidate,
    onError: (e: Error) => setError(e.message),
  })

  const doneMut = useMutation({
    mutationFn: ({ id, done }: { id: string; done: boolean }) =>
      done ? remindersApi.complete(id) : remindersApi.reopen(id),
    onSuccess: invalidate,
    onError: (e: Error) => setError(e.message),
  })

  const evaluateMut = useMutation({
    mutationFn: () => remindersApi.evaluate(),
    onSuccess: (result) => {
      invalidate()
      setError(null)
      notify(`Evaluación completada: ${result.notificationsCreated} avisos generados.`, 'info')
    },
    onError: (e: Error) => setError(e.message),
  })


  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-x-4 gap-y-2">
        <h1 className="text-lg font-bold tracking-wide text-tc-text uppercase">Recordatorios</h1>
        <div className="flex items-center gap-1">
          <Button size="sm" onClick={() => { setEditId(null); setShowModal(true) }}>
            <span className="sm:hidden">Nuevo</span>
            <span className="hidden sm:inline">Nuevo recordatorio</span>
          </Button>
          <RowMenu
            label="Más opciones"
            items={[{ label: evaluateMut.isPending ? 'Evaluando…' : 'Evaluar vencidos ahora', onSelect: () => evaluateMut.mutate() }]}
          />
        </div>
      </div>

      {error && (
        <div className="border border-red-200 bg-red-50 px-4 py-2 text-sm text-red-700 flex items-center justify-between">
          <span>{error}</span>
          <button className="text-xs underline" onClick={() => setError(null)}>Cerrar</button>
        </div>
      )}

      <Tabs
        label="Estado de los recordatorios"
        idPrefix="reminders"
        value={status}
        onChange={v => { setStatus(v); setPage(1) }}
        items={[{ id: 'pending', label: 'Pendientes' }, { id: 'completed', label: 'Cumplidos' }]}
      />

      <DataTable<ReminderListDto>
        label="Recordatorios"
        columns={sortableWhen(COLUMNS, (data?.total ?? 0) <= (data?.pageSize ?? 10))}
        rows={data?.items ?? []}
        getRowId={r => r.id}
        actions={r => (
          <>
            <RowAction tone="purple" onClick={() => doneMut.mutate({ id: r.id, done: !r.completedAt })}>
              {r.completedAt ? 'Reabrir' : <><span className="md:hidden">Cumplido</span><span className="hidden md:inline">Marcar cumplido</span></>}
            </RowAction>
            <RowAction tone="purple" onClick={() => { setEditId(r.id); setShowModal(true) }}>Editar</RowAction>
            <RowAction tone="danger" onClick={async () => { if (await confirmDialog('¿Eliminar este recordatorio?', { danger: true })) deleteMut.mutate(r.id) }}>Eliminar</RowAction>
          </>
        )}
        renderCard={r => (
          <>
            <div className="flex flex-wrap items-center gap-x-2">
              <span className="font-mono text-sm tracking-widest text-tc-text">{r.vehiclePlate}</span>
              {dueSoon(r) && (
                <span className="rounded-md border border-amber-300 bg-amber-50 px-1.5 py-0.5 text-[11px] font-bold tracking-wider uppercase text-amber-700">Próximo</span>
              )}
            </div>
            <p className="text-sm text-tc-text">{r.title}</p>
            <p className="text-xs text-tc-text-secondary">
              {getReminderKindLabel(r.kind)}
              {r.dueDate && ` · vence ${formatDateOnly(r.dueDate)}`}
              {r.dueKm != null && ` · ${r.dueKm.toLocaleString('es-AR')} km`}
            </p>
          </>
        )}
        loading={isLoading}
        emptyMessage={status === 'pending' ? 'No hay recordatorios pendientes.' : 'Todavía no hay recordatorios cumplidos.'}
      />

      {data && <Pagination page={page} pageSize={data.pageSize} total={data.total} noun="recordatorios" onPage={setPage} />}

      {showModal && (
        <ReminderFormModal
          id={editId}
          onClose={() => setShowModal(false)}
          onSaved={() => { setShowModal(false); invalidate() }}
        />
      )}
    </div>
  )
}
