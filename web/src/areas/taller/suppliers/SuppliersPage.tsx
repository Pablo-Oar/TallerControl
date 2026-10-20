import { useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { suppliersApi, type SupplierDto, type VatCondition } from '../../../lib/api/suppliers'
import { Button } from '../../../components/ui/Button'
import { FormField, FormSelect } from '../../../components/ui/FormField'
import { Modal } from '../../../components/ui/Modal'
import { ApiError } from '../../../lib/api/client'

const VAT_LABELS: Record<VatCondition, string> = {
  responsable_inscripto: 'Responsable Inscripto',
  monotributo: 'Monotributista',
  exento: 'Exento',
  consumidor_final: 'Consumidor Final',
  no_categorizado: 'No categorizado',
}

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

  const { data, isLoading } = useQuery({
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
    if (!confirm(`¿Dar de baja al proveedor "${s.name}"?`)) return
    try {
      await suppliersApi.delete(s.id, 'Baja manual')
      queryClient.invalidateQueries({ queryKey: ['suppliers'] })
    } catch (err) {
      alert(err instanceof ApiError ? err.detail : 'Error al dar de baja')
    }
  }

  const showForm = creating || editing !== null
  const totalPages = data ? Math.ceil(data.total / data.pageSize) : 1

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

      {isLoading && (
        <div className="border border-tc-border bg-white">
          {[...Array(4)].map((_, i) => (
            <div key={i} className="h-10 animate-pulse border-b border-tc-border bg-tc-surface last:border-0" />
          ))}
        </div>
      )}

      {!isLoading && data && data.items.length === 0 && (
        <div className="border border-dashed border-tc-border px-4 py-10 text-center text-sm text-tc-text-muted">
          No hay proveedores.
        </div>
      )}

      {!isLoading && data && data.items.length > 0 && (
        <div className="border border-tc-border bg-white overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="bg-tc-stub-bg border-b border-tc-border">
                <th className="px-4 py-2 text-left text-[10px] font-bold tracking-[0.14em] uppercase text-tc-text-muted">Nombre</th>
                <th className="px-4 py-2 text-left text-[10px] font-bold tracking-[0.14em] uppercase text-tc-text-muted">CUIT</th>
                <th className="px-4 py-2 text-left text-[10px] font-bold tracking-[0.14em] uppercase text-tc-text-muted hidden sm:table-cell">Condición IVA</th>
                <th className="px-4 py-2" />
              </tr>
            </thead>
            <tbody>
              {data.items.map(s => (
                <tr key={s.id} className="border-b border-tc-border last:border-0 hover:bg-tc-surface">
                  <td className="px-4 py-2.5 font-medium text-tc-text">{s.name}</td>
                  <td className="px-4 py-2.5 font-mono tabular-nums text-tc-text-secondary whitespace-nowrap">{s.cuit ?? '—'}</td>
                  <td className="px-4 py-2.5 text-tc-text-secondary text-xs hidden sm:table-cell">{VAT_LABELS[s.vatCondition]}</td>
                  <td className="px-4 py-2.5 text-right">
                    <div className="flex items-center justify-end gap-3 text-xs">
                      <button onClick={() => openEdit(s)} className="text-tc-purple hover:opacity-70">Editar</button>
                      <button onClick={() => handleDelete(s)} className="text-tc-stamp-red hover:opacity-70">Baja</button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>

          {data.total > data.pageSize && (
            <div className="flex items-center gap-1 border-t border-tc-border px-4 py-2">
              <button
                className="border border-tc-border bg-white px-3 py-1 text-xs disabled:opacity-40 hover:bg-tc-surface"
                disabled={page <= 1}
                onClick={() => setPage(p => p - 1)}
              >
                ← Anterior
              </button>
              <span className="border border-tc-border bg-tc-stub-bg px-3 py-1 text-xs tabular-nums">
                {page} / {totalPages}
              </span>
              <button
                className="border border-tc-border bg-white px-3 py-1 text-xs disabled:opacity-40 hover:bg-tc-surface"
                disabled={page * data.pageSize >= data.total}
                onClick={() => setPage(p => p + 1)}
              >
                Siguiente →
              </button>
            </div>
          )}
        </div>
      )}
    </div>
  )
}
