import { useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { suppliersApi, type SupplierDto, type VatCondition } from '../../../lib/api/suppliers'
import { Button } from '../../../components/ui/Button'
import { FormField, FormSelect } from '../../../components/ui/FormField'
import { Modal } from '../../../components/ui/Modal'
import { DataTable, RowAction, sortableWhen, type DataTableColumn } from '../../../components/ui/DataTable'
import { Pagination } from '../../../components/ui/Pagination'
import { ApiError } from '../../../lib/api/client'
import { confirmDialog, notify } from '../../../components/ui/dialogs'

const VAT_LABELS: Record<VatCondition, string> = {
  responsable_inscripto: 'Responsable Inscripto',
  monotributo: 'Monotributista',
  exento: 'Exento',
  consumidor_final: 'Consumidor Final',
  no_categorizado: 'No categorizado',
}

const COLUMNS: DataTableColumn<SupplierDto>[] = [
  { key: 'name', header: 'Nombre', sortable: true, sortValue: s => s.name, cell: s => <span className="font-medium text-tc-text">{s.name}</span> },
  {
    key: 'cuit',
    header: 'CUIT',
    widthClass: 'w-44',
    cell: s => <span className="whitespace-nowrap font-mono tabular-nums text-tc-text-secondary">{s.cuit ?? '—'}</span>,
  },
  {
    key: 'vat',
    header: 'Condición IVA',
    widthClass: 'w-56',
    sortable: true,
    sortValue: s => VAT_LABELS[s.vatCondition],
    cell: s => <span className="text-xs text-tc-text-secondary">{VAT_LABELS[s.vatCondition]}</span>,
  },
]

const filterInputCls = 'border border-tc-border bg-white px-3 py-1.5 text-sm text-tc-text outline-none focus:border-tc-border-mid transition-colors flex-1'

export function SuppliersPage() {
  const queryClient = useQueryClient()
  const [q, setQ] = useState('')
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [creating, setCreating] = useState(false)
  const [editing, setEditing] = useState<SupplierDto | null>(null)

  const [formName, setFormName] = useState('')
  const [formCuit, setFormCuit] = useState('')
  const [formVat, setFormVat] = useState<VatCondition>('responsable_inscripto')
  const [saving, setSaving] = useState(false)
  const [formError, setFormError] = useState<string | null>(null)

  const { data, isLoading, error, refetch } = useQuery({
    queryKey: ['suppliers', search, page],
    queryFn: () => suppliersApi.search(search || null, page),
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
    setFormError(null)
    setCreating(true)
    setEditing(null)
  }

  function openEdit(s: SupplierDto) {
    setFormName(s.name)
    setFormCuit(s.cuit ?? '')
    setFormVat(s.vatCondition)
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

  async function handleDelete(s: SupplierDto) {
    if (!(await confirmDialog(`¿Dar de baja al proveedor "${s.name}"?`, { danger: true }))) return
    try {
      await suppliersApi.delete(s.id, 'Baja manual')
      queryClient.invalidateQueries({ queryKey: ['suppliers'] })
    } catch (err) {
      notify(err instanceof ApiError ? err.detail : 'Error al dar de baja')
    }
  }

  const showForm = creating || editing !== null

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-bold tracking-widest text-tc-text uppercase">Proveedores</h1>
        <Button size="sm" onClick={openCreate}>Nuevo proveedor</Button>
      </div>

      <form onSubmit={handleSearch} className="flex gap-1">
        <input
          type="search"
          value={q}
          onChange={e => setQ(e.target.value)}
          placeholder="Buscar por nombre o CUIT…"
          className={filterInputCls}
        />
        <Button type="submit" variant="secondary" size="sm">Buscar</Button>
      </form>

      {showForm && (
        <Modal
          title={editing ? 'Editar proveedor' : 'Nuevo proveedor'}
          onClose={() => { setCreating(false); setEditing(null) }}
          className="max-w-md"
        >
          <form onSubmit={handleSave} className="space-y-4">
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
            </div>
            <div className="flex justify-end gap-2 pt-2">
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
        columns={sortableWhen(COLUMNS, (data?.total ?? 0) <= (data?.pageSize ?? 25))}
        rows={data?.items ?? []}
        getRowId={s => s.id}
        actions={s => (
          <>
            <RowAction tone="purple" onClick={() => openEdit(s)}>Editar</RowAction>
            <RowAction tone="danger" onClick={() => handleDelete(s)}>Baja</RowAction>
          </>
        )}
        renderCard={s => (
          <>
            <p className="truncate text-sm font-medium text-tc-text">{s.name}</p>
            <p className="font-mono text-xs tabular-nums text-tc-text-muted">{s.cuit ?? '—'}</p>
            <p className="text-xs text-tc-text-secondary">{VAT_LABELS[s.vatCondition]}</p>
          </>
        )}
        loading={isLoading}
        error={!!error}
        onRetry={() => refetch()}
        emptyMessage="No hay proveedores."
      />

      {data && <Pagination page={page} pageSize={data.pageSize} total={data.total} noun="proveedores" onPage={setPage} />}
    </div>
  )
}
