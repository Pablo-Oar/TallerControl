import { useState } from 'react'
import { Button } from './ui/Button'
import { Modal } from './ui/Modal'
import { ApiError } from '../lib/api/client'
import { useCashAccountChoice } from './CashAccountSelect'

interface Props {
  title: string
  message: string
  confirmLabel: string
  /** Texto del selector según el sentido del dinero (entra o sale). */
  accountLabel: string
  onConfirm: (accountId: string | null) => Promise<void>
  onClose: () => void
}

/** Confirmación de una compra o venta con la elección de la cuenta de caja donde se asienta. */
export function ConfirmWithCashModal({ title, message, confirmLabel, accountLabel, onConfirm, onClose }: Props) {
  const { accountId, field } = useCashAccountChoice(accountLabel)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function submit(e: React.FormEvent) {
    e.preventDefault()
    setBusy(true)
    setError(null)
    try {
      await onConfirm(accountId)
    } catch (err) {
      setError(err instanceof ApiError ? err.detail : 'No se pudo confirmar. Probá de nuevo.')
      setBusy(false)
    }
  }

  return (
    <Modal title={title} onClose={onClose} className="max-w-sm">
      <form onSubmit={submit} className="space-y-4">
        <p className="text-sm text-tc-text-secondary">{message}</p>
        {error && <p role="alert" className="border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{error}</p>}
        {field}
        <div className="flex justify-end gap-2">
          <Button type="button" variant="secondary" size="sm" onClick={onClose} disabled={busy}>Cancelar</Button>
          <Button type="submit" size="sm" loading={busy}>{confirmLabel}</Button>
        </div>
      </form>
    </Modal>
  )
}
