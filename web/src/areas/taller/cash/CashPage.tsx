import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  cashApi,
  type CashAccountDto,
  type CashAccountKind,
  type CashMovementDto,
  type CashMovementReason,
} from '../../../lib/api/cash'
import { Button } from '../../../components/ui/Button'
import { Modal } from '../../../components/ui/Modal'
import { FormField, FormSelect, FormTextarea } from '../../../components/ui/FormField'
import { ApiError } from '../../../lib/api/client'
import { confirmDialog, notify } from '../../../components/ui/dialogs'
import { LoadError } from '../../../components/LoadError'
import { useCan } from '../../../lib/auth/permissions'

const KIND_LABELS: Record<CashAccountKind, string> = { cash: 'Efectivo', bank: 'Banco', digital: 'Digital' }

const REASON_LABELS: Record<CashMovementReason, string> = {
  payment: 'Cobro de orden',
  purchase_payment: 'Pago de compra',
  counter_sale_income: 'Venta de mostrador',
  manual_income: 'Ingreso manual',
  manual_expense: 'Egreso manual',
}

const fmtARS = (n: number) =>
  new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS', minimumFractionDigits: 2 }).format(n)
const fmtWhen = (iso: string) => new Date(iso).toLocaleString('es-AR', { dateStyle: 'short', timeStyle: 'short' })

type Dialog =
  | { kind: 'create' }
  | { kind: 'edit'; account: CashAccountDto }
  | { kind: 'open'; account: CashAccountDto }
  | { kind: 'close'; account: CashAccountDto }
  | { kind: 'movements'; account: CashAccountDto }

export function CashPage() {
  const queryClient = useQueryClient()
  const can = useCan()
  const [dialog, setDialog] = useState<Dialog | null>(null)

  const { data: accounts, isLoading, error, refetch } = useQuery({
    queryKey: ['cash-accounts'],
    queryFn: () => cashApi.listAccounts(),
  })

  const refresh = () => queryClient.invalidateQueries({ queryKey: ['cash-accounts'] })
  const done = () => {
    refresh()
    setDialog(null)
  }

  async function handleDelete(account: CashAccountDto) {
    if (!(await confirmDialog(`¿Dar de baja la cuenta "${account.name}"? Sus movimientos se conservan.`, { danger: true }))) return
    try {
      await cashApi.deleteAccount(account.id)
      refresh()
    } catch (err) {
      notify(err instanceof ApiError ? err.detail : 'No se pudo dar de baja la cuenta')
    }
  }

  const total = (accounts ?? []).filter(a => a.isActive).reduce((sum, a) => sum + a.balance, 0)

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between gap-3">
        <h1 className="text-lg font-bold tracking-wide text-tc-text uppercase">Cuentas de Caja</h1>
        {can('cash.open') && <Button size="sm" onClick={() => setDialog({ kind: 'create' })}>Nueva cuenta</Button>}
      </div>

      {accounts && accounts.length > 0 && (
        <div className="flex items-center justify-between rounded-2xl border border-tc-border bg-white px-4 py-3">
          <span className="text-xs font-bold tracking-wider uppercase text-tc-text-muted">Total en cuentas activas</span>
          <span className="text-lg font-bold tabular-nums text-tc-purple">{fmtARS(total)}</span>
        </div>
      )}

      {isLoading && (
        <div className="rounded-2xl border border-tc-border bg-white">
          {[...Array(3)].map((_, i) => (
            <div key={i} className="h-16 animate-pulse border-b border-tc-border bg-tc-surface last:border-0" />
          ))}
        </div>
      )}

      {error && <LoadError what="las cuentas de caja" onRetry={() => refetch()} />}

      {!isLoading && !error && accounts && accounts.length === 0 && (
        <div className="rounded-2xl border border-dashed border-tc-border px-4 py-10 text-center text-sm text-tc-text-muted">
          No hay cuentas de caja creadas.
        </div>
      )}

      {accounts && accounts.length > 0 && (
        <ul className="space-y-3">
          {accounts.map(account => {
            const open = !!account.sessionOpenedAt
            return (
              <li
                key={account.id}
                className={`rounded-2xl border border-tc-border bg-white p-4 ${account.isActive ? '' : 'opacity-60'}`}
              >
                <div className="flex flex-wrap items-start justify-between gap-3">
                  <div>
                    <p className="font-semibold text-tc-text">{account.name}</p>
                    <p className="text-xs text-tc-text-secondary">{KIND_LABELS[account.kind]}</p>
                    <div className="mt-2 flex flex-wrap items-center gap-2">
                      <span
                        className={`rounded-full border px-2 py-0.5 text-[11px] font-bold tracking-wider uppercase ${
                          account.isActive ? 'border-green-300 bg-green-50 text-green-700' : 'border-tc-border bg-tc-surface text-tc-text-secondary'
                        }`}
                      >
                        {account.isActive ? 'Activa' : 'Inactiva'}
                      </span>
                      <span
                        className={`rounded-full border px-2 py-0.5 text-[11px] font-bold tracking-wider uppercase ${
                          open ? 'border-tc-purple bg-tc-purple-light text-tc-purple-mid' : 'border-tc-border bg-tc-surface text-tc-text-secondary'
                        }`}
                      >
                        {open ? `Sesión abierta · ${fmtWhen(account.sessionOpenedAt!)}` : 'Sesión cerrada'}
                      </span>
                    </div>
                  </div>
                  <div className="text-right">
                    <p className="text-[11px] font-bold tracking-wider uppercase text-tc-text-muted">Saldo</p>
                    <p className={`text-xl font-bold tabular-nums ${account.balance < 0 ? 'text-tc-stamp-red' : 'text-tc-text'}`}>
                      {fmtARS(account.balance)}
                    </p>
                  </div>
                </div>

                <div className="mt-3 flex flex-wrap gap-2">
                  {account.isActive && !open && can('cash.open') && (
                    <Button size="sm" onClick={() => setDialog({ kind: 'open', account })}>Abrir sesión</Button>
                  )}
                  {open && can('cash.close') && (
                    <Button size="sm" onClick={() => setDialog({ kind: 'close', account })}>Cerrar sesión</Button>
                  )}
                  <Button size="sm" variant="secondary" onClick={() => setDialog({ kind: 'movements', account })}>Movimientos</Button>
                  {can('cash.open') && (
                    <>
                      <Button size="sm" variant="secondary" onClick={() => setDialog({ kind: 'edit', account })}>Editar</Button>
                      <Button size="sm" variant="ghost" onClick={() => handleDelete(account)}>Dar de baja</Button>
                    </>
                  )}
                </div>
              </li>
            )
          })}
        </ul>
      )}

      {dialog?.kind === 'create' && <AccountFormModal onDone={done} onClose={() => setDialog(null)} />}
      {dialog?.kind === 'edit' && <AccountFormModal account={dialog.account} onDone={done} onClose={() => setDialog(null)} />}
      {dialog?.kind === 'open' && <OpenSessionModal account={dialog.account} onDone={done} onClose={() => setDialog(null)} />}
      {dialog?.kind === 'close' && <CloseSessionModal account={dialog.account} onDone={done} onClose={() => setDialog(null)} />}
      {dialog?.kind === 'movements' && (
        <MovementsModal account={dialog.account} canAdjust={can('cash.adjust')} onChanged={refresh} onClose={() => setDialog(null)} />
      )}
    </div>
  )
}

function ErrorBox({ message }: { message: string | null }) {
  return message ? (
    <p role="alert" className="border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{message}</p>
  ) : null
}

function AccountFormModal({ account, onDone, onClose }: { account?: CashAccountDto; onDone: () => void; onClose: () => void }) {
  const [name, setName] = useState(account?.name ?? '')
  const [kind, setKind] = useState<CashAccountKind>(account?.kind ?? 'cash')
  const [active, setActive] = useState(account?.isActive ?? true)
  const [error, setError] = useState<string | null>(null)

  const save = useMutation({
    mutationFn: () =>
      account
        ? cashApi.updateAccount(account.id, { name: name.trim(), kind, isActive: active, version: account.version })
        : cashApi.createAccount({ name: name.trim(), kind }),
    onSuccess: onDone,
    onError: err => setError(err instanceof ApiError ? err.detail : 'No se pudo guardar la cuenta'),
  })

  return (
    <Modal title={account ? 'Editar cuenta de caja' : 'Nueva cuenta de caja'} onClose={onClose} className="max-w-sm">
      <form onSubmit={e => { e.preventDefault(); setError(null); save.mutate() }} className="space-y-4">
        <ErrorBox message={error} />
        <FormField label="Nombre" requiredMark value={name} onChange={e => setName(e.target.value)} placeholder="Nombre de la cuenta" maxLength={100} required />
        <FormSelect label="Tipo" value={kind} onChange={e => setKind(e.target.value as CashAccountKind)}>
          <option value="cash">Efectivo</option>
          <option value="bank">Banco</option>
          <option value="digital">Digital</option>
        </FormSelect>
        {account && (
          <label className="flex items-center gap-2 text-sm text-tc-text-secondary">
            <input type="checkbox" checked={active} onChange={e => setActive(e.target.checked)} className="h-4 w-4" />
            Cuenta activa (las inactivas no reciben movimientos nuevos)
          </label>
        )}
        <div className="flex justify-end gap-2">
          <Button type="button" variant="secondary" size="sm" onClick={onClose}>Cancelar</Button>
          <Button type="submit" size="sm" loading={save.isPending}>{account ? 'Guardar' : 'Crear'}</Button>
        </div>
      </form>
    </Modal>
  )
}

function OpenSessionModal({ account, onDone, onClose }: { account: CashAccountDto; onDone: () => void; onClose: () => void }) {
  const [note, setNote] = useState('')
  const [error, setError] = useState<string | null>(null)
  const open = useMutation({
    mutationFn: () => cashApi.openSession(account.id, { openingNote: note.trim() || null }),
    onSuccess: onDone,
    onError: err => setError(err instanceof ApiError ? err.detail : 'No se pudo abrir la sesión'),
  })
  return (
    <Modal title={`Abrir sesión · ${account.name}`} onClose={onClose} className="max-w-sm">
      <form onSubmit={e => { e.preventDefault(); setError(null); open.mutate() }} className="space-y-4">
        <p className="text-sm text-tc-text-secondary">
          Saldo al abrir: <b className="tabular-nums">{fmtARS(account.balance)}</b>. Si no coincide con lo que hay en la caja, ajustalo antes desde <b>Movimientos</b>.
        </p>
        <ErrorBox message={error} />
        <FormTextarea label="Nota de apertura (opcional)" rows={2} value={note} onChange={e => setNote(e.target.value)} maxLength={500} />
        <div className="flex justify-end gap-2">
          <Button type="button" variant="secondary" size="sm" onClick={onClose}>Cancelar</Button>
          <Button type="submit" size="sm" loading={open.isPending}>Abrir sesión</Button>
        </div>
      </form>
    </Modal>
  )
}

function CloseSessionModal({ account, onDone, onClose }: { account: CashAccountDto; onDone: () => void; onClose: () => void }) {
  const [counted, setCounted] = useState('')
  const [note, setNote] = useState('')
  const [error, setError] = useState<string | null>(null)
  const countedNum = counted.trim() === '' ? null : Number(counted)
  const difference = countedNum != null && !Number.isNaN(countedNum) ? countedNum - account.balance : null

  const close = useMutation({
    mutationFn: () =>
      cashApi.closeSession(account.id, {
        closingNote: note.trim() || null,
        countedAmount: countedNum != null && !Number.isNaN(countedNum) ? countedNum : null,
      }),
    onSuccess: onDone,
    onError: err => setError(err instanceof ApiError ? err.detail : 'No se pudo cerrar la sesión'),
  })

  return (
    <Modal title={`Cerrar sesión · ${account.name}`} onClose={onClose} className="max-w-sm">
      <form onSubmit={e => { e.preventDefault(); setError(null); close.mutate() }} className="space-y-4">
        <div className="flex items-center justify-between rounded-xl border border-tc-border bg-tc-surface px-4 py-2.5">
          <span className="text-xs font-bold tracking-wider uppercase text-tc-text-muted">Saldo esperado</span>
          <span className="font-bold tabular-nums text-tc-purple">{fmtARS(account.balance)}</span>
        </div>
        <ErrorBox message={error} />
        <FormField
          label="Efectivo contado (opcional)"
          type="number"
          step="0.01"
          inputClassName="tabular-nums"
          value={counted}
          onChange={e => setCounted(e.target.value)}
          hint={
            difference == null
              ? 'Contá lo que hay en la caja: el sistema calcula la diferencia.'
              : difference === 0
                ? 'Coincide con lo esperado.'
                : `Diferencia: ${difference > 0 ? '+' : ''}${fmtARS(difference)}`
          }
        />
        <FormTextarea label="Nota de cierre (opcional)" rows={2} value={note} onChange={e => setNote(e.target.value)} maxLength={500} />
        <div className="flex justify-end gap-2">
          <Button type="button" variant="secondary" size="sm" onClick={onClose}>Cancelar</Button>
          <Button type="submit" size="sm" loading={close.isPending}>Cerrar sesión</Button>
        </div>
      </form>
    </Modal>
  )
}

function MovementsModal({
  account,
  canAdjust,
  onChanged,
  onClose,
}: {
  account: CashAccountDto
  canAdjust: boolean
  onChanged: () => void
  onClose: () => void
}) {
  const queryClient = useQueryClient()
  const [direction, setDirection] = useState<'in' | 'out'>('in')
  const [amount, setAmount] = useState('')
  const [note, setNote] = useState('')
  const [error, setError] = useState<string | null>(null)

  const { data: movements, isLoading, error: loadError, refetch } = useQuery({
    queryKey: ['cash-movements', account.id],
    queryFn: () => cashApi.getMovements(account.id),
  })

  const add = useMutation({
    mutationFn: () => cashApi.addMovement(account.id, { amount: Number(amount), direction, note: note.trim() }),
    onSuccess: () => {
      setAmount('')
      setNote('')
      queryClient.invalidateQueries({ queryKey: ['cash-movements', account.id] })
      onChanged()
    },
    onError: err => setError(err instanceof ApiError ? err.detail : 'No se pudo registrar el movimiento'),
  })

  const sign = (m: CashMovementDto) => (m.direction === 'in' ? 1 : -1)
  const balance = (movements ?? []).reduce((sum, m) => sum + sign(m) * m.amount, 0)

  return (
    <Modal title={`Movimientos · ${account.name}`} onClose={onClose} className="max-w-2xl">
      <div className="space-y-4">
        <div className="flex items-center justify-between rounded-xl border border-tc-border bg-tc-surface px-4 py-2.5">
          <span className="text-xs font-bold tracking-wider uppercase text-tc-text-muted">Saldo</span>
          <span className="font-bold tabular-nums text-tc-purple">{fmtARS(movements ? balance : account.balance)}</span>
        </div>

        {canAdjust && account.isActive && (
          <form
            onSubmit={e => { e.preventDefault(); setError(null); add.mutate() }}
            className="space-y-3 rounded-xl border border-tc-border p-3"
          >
            <p className="text-[11px] font-bold tracking-[0.14em] uppercase text-tc-text-muted">Ajuste manual</p>
            <ErrorBox message={error} />
            <div className="grid grid-cols-2 gap-3">
              <FormSelect label="Tipo" value={direction} onChange={e => setDirection(e.target.value as 'in' | 'out')}>
                <option value="in">Ingreso</option>
                <option value="out">Egreso</option>
              </FormSelect>
              <FormField
                label="Monto"
                type="number"
                step="0.01"
                min="0.01"
                inputClassName="tabular-nums"
                value={amount}
                onChange={e => setAmount(e.target.value)}
                required
              />
            </div>
            <FormField label="Motivo" requiredMark value={note} onChange={e => setNote(e.target.value)} maxLength={500} required />
            <div className="flex justify-end">
              <Button type="submit" size="sm" loading={add.isPending}>Registrar movimiento</Button>
            </div>
          </form>
        )}

        {isLoading && <p className="text-sm text-tc-text-muted">Cargando movimientos…</p>}
        {loadError && <LoadError what="los movimientos" onRetry={() => refetch()} />}
        {movements && movements.length === 0 && <p className="text-sm text-tc-text-muted">Todavía no hay movimientos.</p>}
        {movements && movements.length > 0 && (
          <div className="max-h-80 overflow-auto rounded-xl border border-tc-border">
            <table className="w-full text-sm">
              <thead className="sticky top-0 bg-tc-surface">
                <tr>
                  <th className="px-3 py-2 text-left text-[11px] font-bold tracking-[0.14em] uppercase text-tc-text-muted">Fecha</th>
                  <th className="px-3 py-2 text-left text-[11px] font-bold tracking-[0.14em] uppercase text-tc-text-muted">Concepto</th>
                  <th className="px-3 py-2 text-right text-[11px] font-bold tracking-[0.14em] uppercase text-tc-text-muted">Monto</th>
                </tr>
              </thead>
              <tbody>
                {movements.map(m => (
                  <tr key={m.id} className="border-t border-tc-border">
                    <td className="whitespace-nowrap px-3 py-2 tabular-nums text-tc-text-secondary">{fmtWhen(m.createdAt)}</td>
                    <td className="px-3 py-2 text-tc-text-secondary">
                      {REASON_LABELS[m.reason] ?? m.reason}
                      {m.note && <span className="block text-xs text-tc-text-muted">{m.note}</span>}
                    </td>
                    <td className={`whitespace-nowrap px-3 py-2 text-right font-semibold tabular-nums ${m.direction === 'in' ? 'text-tc-done-text' : 'text-tc-stamp-red'}`}>
                      {m.direction === 'in' ? '+' : '−'} {fmtARS(m.amount)}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        <div className="flex justify-end">
          <Button variant="secondary" size="sm" onClick={onClose}>Cerrar</Button>
        </div>
      </div>
    </Modal>
  )
}
