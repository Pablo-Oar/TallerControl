import { useRef, useState, type FormEvent, type ReactNode } from 'react'
import { Button } from './Button'
import { Modal } from './Modal'

/**
 * Patrón único de edición en diálogo (UI-R2 F2, "Edit Profile"): formulario con estado "con cambios",
 * confirmación al descartar (Escape, clic fuera, × o Cancelar con cambios sin guardar) y botón con loading.
 * El formulario lo maneja el llamador (React Hook Form + Zod); acá solo está el marco y el comportamiento.
 */
interface EditDialogProps {
  title: string
  onClose: () => void
  onSubmit: (e: FormEvent<HTMLFormElement>) => void
  /** Hay cambios sin guardar (`formState.isDirty`). */
  dirty: boolean
  saving?: boolean
  /** Error del servidor, mostrado sobre los botones. */
  error?: string | null
  submitLabel?: string
  /** En edición, "Guardar" se habilita solo cuando hay cambios. */
  requireChanges?: boolean
  /** Acción secundaria a la izquierda del pie (p. ej. "Eliminar"). */
  footerStart?: ReactNode
  className?: string
  children: ReactNode
}

export function EditDialog({
  title,
  onClose,
  onSubmit,
  dirty,
  saving = false,
  error,
  submitLabel = 'Guardar',
  requireChanges = false,
  footerStart,
  className,
  children,
}: EditDialogProps) {
  const [confirmDiscard, setConfirmDiscard] = useState(false)
  const formRef = useRef<HTMLFormElement>(null)

  // El botón enfocado desaparece con el aviso: el foco vuelve al primer campo para no perderlo en el body.
  function keepEditing() {
    setConfirmDiscard(false)
    requestAnimationFrame(() => formRef.current?.querySelector<HTMLElement>('input, select, textarea')?.focus())
  }

  function requestClose() {
    if (dirty && !saving) setConfirmDiscard(true)
    else onClose()
  }

  return (
    <Modal title={title} onClose={requestClose} className={className}>
      <form ref={formRef} onSubmit={onSubmit} noValidate className="space-y-4">
        {children}

        {error && (
          <p role="alert" className="border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
            {error}
          </p>
        )}

        {confirmDiscard && (
          <div role="alert" className="space-y-2 border border-amber-200 bg-amber-50 p-3">
            <p className="text-sm font-medium text-amber-900">Hay cambios sin guardar. ¿Descartarlos?</p>
            <div className="flex gap-2">
              <Button type="button" size="sm" variant="secondary" data-autofocus onClick={keepEditing}>
                Seguir editando
              </Button>
              <Button type="button" size="sm" variant="danger" onClick={onClose}>
                Descartar cambios
              </Button>
            </div>
          </div>
        )}

        <div className="sticky -bottom-5 -mx-5 -mb-5 mt-2 flex items-center justify-between gap-3 border-t border-tc-border bg-white px-5 pt-3 pb-[max(0.75rem,env(safe-area-inset-bottom))] shadow-[0_-6px_8px_-6px_rgb(0_0_0/0.12)]">
          <div>{footerStart}</div>
          <div className="flex gap-3">
            <Button type="button" variant="secondary" size="sm" onClick={requestClose} disabled={saving}>
              Cancelar
            </Button>
            <Button type="submit" size="sm" loading={saving} disabled={requireChanges && !dirty}>
              {submitLabel}
            </Button>
          </div>
        </div>
      </form>
    </Modal>
  )
}
