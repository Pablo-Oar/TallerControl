import { useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { cashApi, type CashAccountDto, type CreateCashAccountRequest, type CashAccountKind } from '../../../lib/api/cash'
import { Button } from '../../../components/ui/Button'
import { ApiError } from '../../../lib/api/client'

const KIND_LABELS: Record<CashAccountKind, string> = {
  cash: 'Efectivo',
  bank: 'Banco',
  digital: 'Digital',
}

export function CashPage() {
  const queryClient = useQueryClient()
  const [creating, setCreating] = useState(false)
  const [newName, setNewName] = useState('')
  const [newKind, setNewKind] = useState<CashAccountKind>('cash')
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [openingId, setOpeningId] = useState<string | null>(null)
  const [closingId, setClosingId] = useState<string | null>(null)

  const { data: accounts, isLoading } = useQuery({
    queryKey: ['cash-accounts'],
    queryFn: () => cashApi.listAccounts(),
  })

  async function handleCreate(e: React.FormEvent) {
    e.preventDefault()
    setSaving(true)
    setError(null)
    try {
      const req: CreateCashAccountRequest = { name: newName.trim(), kind: newKind }
      await cashApi.createAccount(req)
      queryClient.invalidateQueries({ queryKey: ['cash-accounts'] })
      setCreating(false)
      setNewName('')
      setNewKind('cash')
    } catch (err) {
      setError(err instanceof ApiError ? err.detail : 'Error al crear la cuenta')
    } finally {
      setSaving(false)
    }
  }

  async function handleOpen(account: CashAccountDto) {
    setOpeningId(account.id)
    try {
      await cashApi.openSession(account.id, {})
      queryClient.invalidateQueries({ queryKey: ['cash-accounts'] })
    } catch (err) {
      alert(err instanceof ApiError ? err.detail : 'Error al abrir la sesión')
    } finally {
      setOpeningId(null)
    }
  }

  async function handleClose(account: CashAccountDto) {
    if (!confirm('¿Cerrar la sesión de caja actual?')) return
    setClosingId(account.id)
    try {
      await cashApi.closeSession(account.id, {})
      queryClient.invalidateQueries({ queryKey: ['cash-accounts'] })
    } catch (err) {
      alert(err instanceof ApiError ? err.detail : 'Error al cerrar la sesión')
    } finally {
      setClosingId(null)
    }
  }

  return (
    <div>
      <div className="mb-6 flex items-center justify-between">
        <h1 className="text-2xl font-semibold">Cuentas de Caja</h1>
        <Button onClick={() => setCreating(true)}>Nueva cuenta</Button>
      </div>

      {creating && (
        <form onSubmit={handleCreate} className="mb-6 rounded-lg border border-slate-200 bg-white p-4">
          <h2 className="mb-4 text-sm font-semibold text-slate-700">Nueva cuenta de caja</h2>
          {error && <p className="mb-3 text-sm text-red-600">{error}</p>}
          <div className="mb-3 flex gap-3">
            <input
              type="text"
              value={newName}
              onChange={e => setNewName(e.target.value)}
              placeholder="Nombre de la cuenta"
              required
              className="flex-1 rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-blue-500"
            />
            <select
              value={newKind}
              onChange={e => setNewKind(e.target.value as CashAccountKind)}
              className="rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-blue-500"
            >
              <option value="cash">Efectivo</option>
              <option value="bank">Banco</option>
              <option value="digital">Digital</option>
            </select>
          </div>
          <div className="flex gap-2">
            <Button type="submit" loading={saving}>Crear</Button>
            <Button type="button" variant="secondary" onClick={() => setCreating(false)}>Cancelar</Button>
          </div>
        </form>
      )}

      {isLoading && (
        <div className="flex justify-center py-12">
          <div className="h-6 w-6 animate-spin rounded-full border-2 border-blue-600 border-t-transparent" />
        </div>
      )}

      {!isLoading && accounts && accounts.length === 0 && (
        <p className="py-8 text-center text-sm text-slate-500">No hay cuentas de caja creadas.</p>
      )}

      {!isLoading && accounts && accounts.length > 0 && (
        <div className="space-y-3">
          {accounts.map(account => (
            <div
              key={account.id}
              className="flex items-center justify-between rounded-lg border border-slate-200 bg-white px-4 py-3"
            >
              <div>
                <p className="font-medium">{account.name}</p>
                <p className="text-sm text-slate-500">{KIND_LABELS[account.kind]}</p>
              </div>
              <div className="flex items-center gap-2">
                <span
                  className={`rounded-full px-2 py-0.5 text-xs font-medium ${
                    account.isActive
                      ? 'bg-green-100 text-green-700'
                      : 'bg-slate-100 text-slate-500'
                  }`}
                >
                  {account.isActive ? 'Activa' : 'Inactiva'}
                </span>
                <Button
                  variant="secondary"
                  loading={openingId === account.id}
                  onClick={() => handleOpen(account)}
                >
                  Abrir sesión
                </Button>
                <Button
                  variant="secondary"
                  loading={closingId === account.id}
                  onClick={() => handleClose(account)}
                >
                  Cerrar sesión
                </Button>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  )
}
