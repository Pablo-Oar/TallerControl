import { useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { suppliersApi, type SupplierDto, type VatCondition } from '../../../lib/api/suppliers'
import { Button } from '../../../components/ui/Button'
import { FormField, FormSelect, FormTextarea } from '../../../components/ui/FormField'
import { Modal } from '../../../components/ui/Modal'
import { DataTable, RowAction, sortableWhen, type DataTableColumn } from '../../../components/ui/DataTable'
import { Pagination } from '../../../components/ui/Pagination'
import { ApiError } from '../../../lib/api/client'
import { spanishInvalid } from '../../../lib/formValidity'
import { notify } from '../../../components/ui/dialogs'

const VAT_LABELS: Record<VatCondition, string> = {
  responsable_inscripto: 'Responsable Inscripto',
  monotributo: 'Monotributista',
  exento: 'Exento',
  consumidor_final: 'Consumidor Final',
  no_responsable: 'No Responsable',
}

const COLUMNS: DataTableColumn<SupplierDto>[] = [
  { key: 'name', header: 'Nombre', sortable: true, sortValue: s => s.name, cell: s => <span className="font-medium text-tc-text">{s.name}</span> },
  {
    key: 'cuit',
    header: 'CUIT',
    widthClass: 'w-44',
    cell: s => <span className="whitespace-nowrap font-mono tabular-nums text-tc-text-secondary">{s.cuit ?? '—'}</span>,
  },
  { key: 'phone', header: 'Teléfono', widthClass: 'w-40', cell: s => <span className="whitespace-nowrap tabular-nums text-tc-text-secondary">{s.phone ?? '—'}</span> },
  { key: 'email', header: 'Email', cell: s => <span className="truncate text-tc-text-secondary">{s.email ?? '—'}</span> },
  {
    key: 'vat',
    header: 'Condición IVA',
    widthClass: 'w-56',
    sortable: true,
    sortValue: s => VAT_LABELS[s.vatCondition],
    cell: s => <span className="text-xs text-tc-text-secondary">{VAT_LABELS[s.vatCondition]}</span>,
  },
]

const filterInputCls = 'max-md:min-h-11 min-w-0 flex-1 basis-48 border border-tc-border bg-white px-3 py-1.5 text-sm text-tc-text outline-none focus:border-tc-border-mid transition-colors'

export function SuppliersPage() {
  const queryClient = useQueryClient()
  const [q, setQ] = useState('')
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [creating, setCreating] = useState(false)
  const [editing, setEditing] = useState<SupplierDto | null>(null)
  const [showDeleted, setShowDeleted] = useState(false)
  const [deleting, setDeleting] = useState<SupplierDto | null>(null)
  const [deleteReason, setDeleteReason] = useState('')
  const [deleteBusy, setDeleteBusy] = useState(false)
  const [formAddress, setFormAddress] = useState('')
  const [formPhone, setFormPhone] = useState('')
  const [formEmail, setFormEmail] = useState('')
  const [formNotes, setFormNotes] = useState('')

  const [formName, setFormName] = useState('')
  const [formCuit, setFormCuit] = useState('')
  const [formVat, setFormVat] = useState<VatCondition>('responsable_inscripto')
  const [saving, setSaving] = useState(false)
  const [formError, setFormError] = useState<string | null>(null)

  const { data, isLoading, error, refetch } = useQuery({
    queryKey: ['suppliers', search, page, showDeleted],
    queryFn: () => suppliersApi.search(search || null, page, 10, showDeleted),
  })

  function handleSearch(e: React.FormEvent) {
    e.preventDefault()
    setSearch(q)
    setPage(1)
  }

  function openCreate() {
    setFormName('')
    setFormCuit('')
    setFormVat('responsable_inscripto')
    setFormAddress('')
    setFormPhone('')
    setFormEmail('')
    setFormNotes('')
    setFormError(null)
    setCreating(true)
    setEditing(null)
  }

  function openEdit(s: SupplierDto) {
    setFormName(s.name)
    setFormCuit(s.cuit ?? '')
    setFormVat(s.vatCondition)
    setFormAddress(s.address ?? '')
    setFormPhone(s.phone ?? '')
    setFormEmail(s.email ?? '')
    setFormNotes(s.notes ?? '')
    setFormError(null)
    setEditing(s)
    setCreating(false)
  }

  async function handleSave(e: React.FormEvent) {
    e.preventDefault()
    setSaving(true)
    setFormError(null)
    try {
      const body = {
        name: formName.trim(),
        cuit: formCuit.trim() || null,
        vatCondition: formVat,
        address: formAddress.trim() || null,
        phone: formPhone.trim() || null,
        email: formEmail.trim() || null,
        notes: formNotes.trim() || null,
      }
      if (editing) {
        await suppliersApi.update(editing.id, body, editing.version)
      } else {
        await suppliersApi.create(body)
      }
      queryClient.invalidateQueries({ queryKey: ['suppliers'] })
      setCreating(false)
      setEditing(null)
    } catch (err) {
      setFormError(err instanceof ApiError ? err.detail : 'Error al guardar')
    } finally {
      setSaving(false)
    }
  }

  async function openDelete(s: SupplierDto) {
    // El detalle trae cuántas compras vigentes tiene, para avisar antes de dar de baja.
    try {
      setDeleting(await suppliersApi.get(s.id))
      setDeleteReason('')
    } catch (err) {
      notify(err instanceof ApiError ? err.detail : 'No se pudo leer el proveedor')
    }
  }

  async function confirmDelete() {
    if (!deleting || !deleteReason.trim()) return
    setDeleteBusy(true)
    try {
      await suppliersApi.delete(deleting.id, deleteReason.trim())
      queryClient.invalidateQueries({ queryKey: ['suppliers'] })
      setDeleting(null)
    } catch (err) {
      notify(err instanceof ApiError ? err.detail : 'Error al dar de baja')
    } finally {
      setDeleteBusy(false)
    }
  }

  async function handleRestore(s: SupplierDto) {
    try {
      await suppliersApi.restore(s.id)
      queryClient.invalidateQueries({ queryKey: ['suppliers'] })
    } catch (err) {
      notify(err instanceof ApiError ? err.detail : 'Error al restaurar')
    }
  }

  const showForm = creating || editing !== null

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-x-4 gap-y-2">
        <h1 className="text-lg font-bold tracking-wide text-tc-text uppercase">Proveedores</h1>
        <Button size="sm" onClick={openCreate}>Nuevo proveedor</Button>
      </div>

      <form onSubmit={handleSearch} className="flex flex-wrap items-center gap-2">
        <input
          type="search"
          value={q}
          onChange={e => setQ(e.target.value)}
          placeholder="Buscar por nombre o CUIT…"
          className={filterInputCls}
        />
        <Button type="submit" variant="secondary" size="sm" className="shrink-0">Buscar</Button>
        <label className="flex items-center gap-1.5 whitespace-nowrap text-sm text-tc-text-secondary">
          <input type="checkbox" className="h-4 w-4" checked={showDeleted} onChange={e => { setShowDeleted(e.target.checked); setPage(1) }} />
          Ver dados de baja
        </label>
      </form>

      {showForm && (
        <Modal
          title={editing ? 'Editar proveedor' : 'Nuevo proveedor'}
          onClose={() => { setCreating(false); setEditing(null) }}
          className="max-w-md"
        >
          <form onSubmit={handleSave} onInvalidCapture={spanishInvalid(setFormError)} className="space-y-4">
            {formError && (
              <p role="alert" className="border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{formError}</p>
            )}
            <div className="grid grid-cols-2 gap-3">
              <FormField
                label="Nombre"
                requiredMark
                type="text"
                value={formName}
                onChange={e => setFormName(e.target.value)}
                required
              />
              <FormField
                label="CUIT"
                type="text"
                value={formCuit}
                onChange={e => setFormCuit(e.target.value)}
                placeholder="xx-xxxxxxxx-x"
                inputClassName="font-mono tabular-nums"
              />
              <FormSelect
                label="Condición IVA"
                requiredMark
                className="col-span-2"
                value={formVat}
                onChange={e => setFormVat(e.target.value as VatCondition)}
              >
                {(Object.entries(VAT_LABELS) as [VatCondition, string][]).map(([k, v]) => (
                  <option key={k} value={k}>{v}</option>
                ))}
              </FormSelect>
              <FormField label="Teléfono" type="tel" value={formPhone} onChange={e => setFormPhone(e.target.value)} />
              <FormField label="Email" type="email" value={formEmail} onChange={e => setFormEmail(e.target.value)} />
              <FormField label="Dirección" className="col-span-2" value={formAddress} onChange={e => setFormAddress(e.target.value)} />
              <FormTextarea label="Notas" rows={2} className="col-span-2" value={formNotes} onChange={e => setFormNotes(e.target.value)} />
            </div>
            <div className="sticky -bottom-5 -mx-5 -mb-5 mt-2 flex justify-end gap-2 border-t border-tc-border bg-white px-5 pt-3 pb-[max(0.75rem,env(safe-area-inset-bottom))] shadow-[0_-6px_8px_-6px_rgb(0_0_0/0.12)]">
              <Button type="button" variant="secondary" size="sm" onClick={() => { setCreating(false); setEditing(null) }}>
                Cancelar
              </Button>
              <Button type="submit" size="sm" loading={saving}>Guardar</Button>
            </div>
          </form>
        </Modal>
      )}

      <DataTable<SupplierDto>
        label="Proveedores"
        columns={sortableWhen(COLUMNS, (data?.total ?? 0) <= (data?.pageSize ?? 10))}
        rows={data?.items ?? []}
        getRowId={s => s.id}
        actions={s =>
          showDeleted ? (
            <RowAction tone="purple" onClick={() => handleRestore(s)}>Restaurar</RowAction>
          ) : (
            <>
              <RowAction tone="purple" onClick={() => openEdit(s)}>Editar</RowAction>
              <RowAction tone="danger" onClick={() => openDelete(s)}>Baja</RowAction>
            </>
          )
        }
        renderCard={s => (
          <>
            <p className="truncate text-sm font-medium text-tc-text">{s.name}</p>
            <p className="font-mono text-xs tabular-nums text-tc-text-muted">{s.cuit ?? '—'}</p>
            <p className="text-xs text-tc-text-secondary">{VAT_LABELS[s.vatCondition]}</p>
            {(s.phone || s.email) && <p className="truncate text-xs text-tc-text-muted">{[s.phone, s.email].filter(Boolean).join(' · ')}</p>}
          </>
        )}
        loading={isLoading}
        error={!!error}
        onRetry={() => refetch()}
        emptyMessage={showDeleted ? 'No hay proveedores dados de baja.' : 'No hay proveedores.'}
      />

      {data && <Pagination page={page} pageSize={data.pageSize} total={data.total} noun="proveedores" onPage={setPage} />}

      {deleting && (
        <Modal title="Dar de baja al proveedor" onClose={() => setDeleting(null)} className="max-w-sm">
          <div className="space-y-4">
            <p className="text-sm text-tc-text-secondary">
              «{deleting.name}» deja de ofrecerse al cargar compras. Las compras que ya tiene no cambian, y lo podés restaurar desde «Ver dados de baja».
            </p>
            {(deleting.purchaseCount ?? 0) > 0 && (
              <p role="alert" className="border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-800">
                Este proveedor tiene {deleting.purchaseCount} compra{deleting.purchaseCount === 1 ? '' : 's'} vigente{deleting.purchaseCount === 1 ? '' : 's'} (en borrador o confirmada{deleting.purchaseCount === 1 ? '' : 's'}).
              </p>
            )}
            <FormTextarea label="Motivo" requiredMark rows={2} value={deleteReason} onChange={e => setDeleteReason(e.target.value)} />
            <div className="flex justify-end gap-2">
              <Button type="button" variant="secondary" size="sm" onClick={() => setDeleting(null)}>Volver</Button>
              <Button variant="danger" size="sm" loading={deleteBusy} disabled={!deleteReason.trim()} onClick={confirmDelete}>Dar de baja</Button>
            </div>
          </div>
        </Modal>
      )}
    </div>
  )
}
