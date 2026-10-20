import { useEffect, useState } from 'react'
import { Button } from './Button'
import { Modal } from './Modal'

/**
 * Reemplazo de `confirm()` / `alert()` nativos (UI-R2 post-prueba en celular): mismo uso imperativo,
 * pero con el `Modal` del sistema y un aviso propio. Montar <DialogHost /> una vez en la raíz.
 */

interface ConfirmRequest {
  id: number
  message: string
  confirmLabel: string
  danger: boolean
  resolve: (ok: boolean) => void
}
interface Notice {
  id: number
  message: string
  tone: 'error' | 'info'
}

type Listener = () => void
let seq = 0
let confirms: ConfirmRequest[] = []
let notices: Notice[] = []
const listeners = new Set<Listener>()
const emit = () => listeners.forEach(l => l())

export function confirmDialog(message: string, opts: { confirmLabel?: string; danger?: boolean } = {}): Promise<boolean> {
  return new Promise(resolve => {
    confirms = [...confirms, { id: ++seq, message, confirmLabel: opts.confirmLabel ?? 'Aceptar', danger: opts.danger ?? false, resolve }]
    emit()
  })
}

export function notify(message: string, tone: Notice['tone'] = 'error') {
  const id = ++seq
  notices = [...notices, { id, message, tone }]
  emit()
  setTimeout(() => dismiss(id), 7000)
}

function dismiss(id: number) {
  notices = notices.filter(n => n.id !== id)
  emit()
}

function answer(req: ConfirmRequest, ok: boolean) {
  confirms = confirms.filter(c => c.id !== req.id)
  req.resolve(ok)
  emit()
}

export function DialogHost() {
  const [, force] = useState(0)
  useEffect(() => {
    const l = () => force(n => n + 1)
    listeners.add(l)
    return () => {
      listeners.delete(l)
    }
  }, [])

  const current = confirms[0]
  return (
    <>
      {current && (
        <Modal title="Confirmar" onClose={() => answer(current, false)} className="max-w-sm">
          <p className="mb-5 text-sm text-tc-text">{current.message}</p>
          <div className="flex justify-end gap-3">
            <Button variant="secondary" size="sm" onClick={() => answer(current, false)}>
              Cancelar
            </Button>
            <Button variant={current.danger ? 'danger' : 'primary'} size="sm" data-autofocus onClick={() => answer(current, true)}>
              {current.confirmLabel}
            </Button>
          </div>
        </Modal>
      )}
      <div className="pointer-events-none fixed inset-x-0 bottom-0 z-[60] flex flex-col items-center gap-2 p-3">
        {notices.map(n => (
          <div
            key={n.id}
            role={n.tone === 'error' ? 'alert' : 'status'}
            className={[
              'pointer-events-auto flex w-full max-w-md items-start gap-3 border px-4 py-3 text-sm shadow-lg',
              n.tone === 'error' ? 'border-red-200 bg-red-50 text-red-800' : 'border-tc-border bg-white text-tc-text',
            ].join(' ')}
          >
            <span className="flex-1">{n.message}</span>
            <button type="button" onClick={() => dismiss(n.id)} className="min-h-[32px] px-1 text-xs font-bold uppercase" aria-label="Cerrar aviso">
              Cerrar
            </button>
          </div>
        ))}
      </div>
    </>
  )
}
