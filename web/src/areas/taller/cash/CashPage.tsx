import { useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { cashApi, type CashAccountDto, type CreateCashAccountRequest, type CashAccountKind } from '../../../lib/api/cash'
import { Button } from '../../../components/ui/Button'
import { Modal } from '../../../components/ui/Modal'
import { ApiError } from '../../../lib/api/client'

const KIND_LABELS: Record<CashAccountKind, string> = {
  cash: 'Efectivo',
  bank: 'Banco',
  digital: 'Digital',
}

const inputCls = 'w-full border border-tc-border bg-white px-3 py-2 text-sm text-slate-800 outline-none focus:border-tc-border-mid transition-colors'
const labelCls = 'mb-1 block text-xs font-bold tracking-wider uppercase text-slate-500'

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
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-bold tracking-tight text-slate-900">Cuentas de Caja</h1>
        <Button size="sm" onClick={() => setCreating(true)}>Nueva cuenta</Button>
      </div>

      {creating && (
        <Modal title="Nueva cuenta de caja" onClose={() => setCreating(false)} className="max-w-sm">
          <form onSubmit={handleCreate} className="space-y-4">
            {error && (
              <p role="alert" className="border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{error}</p>
            )}
            <div>
              <label className={labelCls}>Nombre *</label>
              <input
                type="text"
                value={newName}
                onChange={e => setNewName(e.target.value)}
                placeholder="Nombre de la cuenta"
                required
                className={inputCls}
              />
            </div>
            <div>
              <label className={labelCls}>Tipo</label>
              <select
                value={newKind}
                onChange={e => setNewKind(e.target.value as CashAccountKind)}
                className={inputCls}
              >
                <option value="cash">Efectivo</option>
                <option value="bank">Banco</option>
                <option value="digital">Digital</option>
              </select>
            </div>
            <div className="flex justify-end gap-2 pt-2">
              <Button type="button" variant="secondary" size="sm" onClick={() => setCreating(false)}>Cancelar</Button>
              <Button type="submit" size="sm" loading={saving}>Crear</Button>
            </div>
          </form>
        </Modal>
      )}

      {isLoading && (
        <div className="border border-tc-border bg-white">
          {[...Array(3)].map((_, i) => (
            <div key={i} className="h-14 animate-pulse border-b border-tc-border bg-tc-surface last:border-0" />
          ))}
        </div>
      )}

      {!isLoading && accounts && accounts.length === 0 && (
        <div className="border border-dashed border-tc-border px-4 py-10 text-center text-sm text-slate-400">
          No hay cuentas de caja creadas.
        </div>
      )}

      {!isLoading && accounts && accounts.length > 0 && (
        <div className="border border-tc-border bg-white overflow-hidden">
          {accounts.map((account, idx) => (
            <div
              key={account.id}
              className={`flex items-center justify-between px-4 py-3 ${idx < accounts.length - 1 ? 'border-b border-tc-border' : ''} hover:bg-tc-surface`}
            >
              <div className="flex items-center gap-3">
                <div className="w-1.5 h-8 bg-tc-stub-bg border border-tc-border" />
                <div>
                  <p className="font-medium text-slate-800">{account.name}</p>
                  <p className="text-xs text-slate-500">{KIND_LABELS[account.kind]}</p>
                </div>
              </div>
              <div className="flex items-center gap-3">
                <span
                  className={`border px-2 py-0.5 text-[10px] font-bold tracking-wider uppercase ${
                    account.isActive
                      ? 'border-green-300 bg-green-50 text-green-700'
                      : 'border-tc-border bg-tc-stub-bg text-slate-500'
                  }`}
                >
                  {account.isActive ? 'Activa' : 'Inactiva'}
                </span>
                <Button
                  variant="secondary"
                  size="sm"
                  loading={openingId === account.id}
                  onClick={() => handleOpen(account)}
                >
                  Abrir sesión
                </Button>
                <Button
                  variant="secondary"
                  size="sm"
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
