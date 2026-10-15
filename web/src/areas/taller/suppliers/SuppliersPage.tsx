import { useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { suppliersApi, type SupplierDto, type VatCondition } from '../../../lib/api/suppliers'
import { Button } from '../../../components/ui/Button'
import { ApiError } from '../../../lib/api/client'

const VAT_LABELS: Record<VatCondition, string> = {
  responsable_inscripto: 'Responsable Inscripto',
  monotributo: 'Monotributista',
  exento: 'Exento',
  consumidor_final: 'Consumidor Final',
  no_categorizado: 'No categorizado',
}

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

  return (
    <div>
      <div className="mb-6 flex items-center justify-between">
        <h1 className="text-2xl font-semibold">Proveedores</h1>
        <Button onClick={openCreate}>Nuevo proveedor</Button>
      </div>

      <form onSubmit={handleSearch} className="mb-4 flex gap-2">
        <input
          type="search"
          value={q}
          onChange={e => setQ(e.target.value)}
          placeholder="Buscar por nombre o CUIT…"
          className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-blue-500"
        />
        <Button type="submit" variant="secondary">Buscar</Button>
      </form>

      {showForm && (
        <form onSubmit={handleSave} className="mb-6 rounded-lg border border-slate-200 bg-white p-4">
          <h2 className="mb-4 text-sm font-semibold text-slate-700">
            {editing ? 'Editar proveedor' : 'Nuevo proveedor'}
          </h2>
          {formError && <p className="mb-3 text-sm text-red-600">{formError}</p>}
          <div className="mb-3 grid grid-cols-2 gap-3">
            <div>
              <label className="mb-1 block text-xs font-medium text-slate-600">Nombre *</label>
              <input
                type="text"
                value={formName}
                onChange={e => setFormName(e.target.value)}
                required
                className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-blue-500"
              />
            </div>
            <div>
              <label className="mb-1 block text-xs font-medium text-slate-600">CUIT</label>
              <input
                type="text"
                value={formCuit}
                onChange={e => setFormCuit(e.target.value)}
                placeholder="xx-xxxxxxxx-x"
                className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-blue-500"
              />
            </div>
            <div>
              <label className="mb-1 block text-xs font-medium text-slate-600">Condición IVA *</label>
              <select
                value={formVat}
                onChange={e => setFormVat(e.target.value as VatCondition)}
                className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-blue-500"
              >
                {(Object.entries(VAT_LABELS) as [VatCondition, string][]).map(([k, v]) => (
                  <option key={k} value={k}>{v}</option>
                ))}
              </select>
            </div>
          </div>
          <div className="flex gap-2">
            <Button type="submit" loading={saving}>Guardar</Button>
            <Button type="button" variant="secondary" onClick={() => { setCreating(false); setEditing(null) }}>
              Cancelar
            </Button>
          </div>
        </form>
      )}

      {isLoading && (
        <div className="flex justify-center py-12">
          <div className="h-6 w-6 animate-spin rounded-full border-2 border-blue-600 border-t-transparent" />
        </div>
      )}

      {!isLoading && data && data.items.length === 0 && (
        <p className="py-8 text-center text-sm text-slate-500">No hay proveedores.</p>
      )}

      {!isLoading && data && data.items.length > 0 && (
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-slate-200 text-left text-xs font-medium uppercase text-slate-500">
                <th className="pb-2 pr-4">Nombre</th>
                <th className="pb-2 pr-4">CUIT</th>
                <th className="pb-2 pr-4">Condición IVA</th>
                <th className="pb-2" />
              </tr>
            </thead>
            <tbody>
              {data.items.map(s => (
                <tr key={s.id} className="border-b border-slate-100">
                  <td className="py-2 pr-4 font-medium">{s.name}</td>
                  <td className="py-2 pr-4 text-slate-600">{s.cuit ?? '—'}</td>
                  <td className="py-2 pr-4 text-slate-600">{VAT_LABELS[s.vatCondition]}</td>
                  <td className="py-2 text-right">
                    <button
                      onClick={() => openEdit(s)}
                      className="mr-3 text-blue-600 hover:underline"
                    >
                      Editar
                    </button>
                    <button
                      onClick={() => handleDelete(s)}
                      className="text-red-500 hover:underline"
                    >
                      Baja
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>

          {data.total > data.pageSize && (
            <div className="mt-4 flex justify-center gap-2">
              <Button
                variant="secondary"
                disabled={page <= 1}
                onClick={() => setPage(p => p - 1)}
              >
                Anterior
              </Button>
              <span className="flex items-center text-sm text-slate-500">
                {page} / {Math.ceil(data.total / data.pageSize)}
              </span>
              <Button
                variant="secondary"
                disabled={page * data.pageSize >= data.total}
                onClick={() => setPage(p => p + 1)}
              >
                Siguiente
              </Button>
            </div>
          )}
        </div>
      )}
    </div>
  )
}
