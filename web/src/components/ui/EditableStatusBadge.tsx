import { useEffect, useId, useRef, useState, type KeyboardEvent } from 'react'
import { StatusBadge } from './StatusBadge'

/**
 * Sello de estado editable (UI-R2 F3, D-25 "Edit Badge"). Muestra el estado actual como `StatusBadge` y, al abrirlo,
 * ofrece SOLO las opciones que el llamador declara válidas (la tabla de transiciones es del dominio, no de la UI).
 * No aplica lógica de negocio: `onSelect` hace el cambio (o delega, p. ej. abrir un diálogo si pide un motivo).
 * Si falla (p. ej. 422) muestra el mensaje y el sello queda en el estado anterior: el estado mostrado siempre sale de
 * `status`, que solo cambia cuando el llamador lo refresca tras un cambio exitoso.
 */

export interface StatusOption {
  value: string
  label: string
}

interface Props {
  /** Estado actual (clave de `StatusBadge`). */
  status: string
  /** Texto del sello; por defecto el de `StatusBadge`. */
  label?: string
  /** Nombre legible del estado para lectores de pantalla (p. ej. "Pendiente"); por defecto la clave. */
  statusText?: string
  options: StatusOption[]
  /** Hace el cambio; si lanza, se muestra `error.detail ?? error.message`. */
  onSelect: (value: string) => void | Promise<void>
  className?: string
}

export function EditableStatusBadge({ status, label, statusText, options, onSelect, className = '' }: Props) {
  const menuId = useId()
  const triggerRef = useRef<HTMLButtonElement>(null)
  const wrapRef = useRef<HTMLDivElement>(null)
  const [open, setOpen] = useState(false)
  const [active, setActive] = useState(0)
  const [pending, setPending] = useState(false)
  const [error, setError] = useState<string | null>(null)

  // Clic fuera cierra el menú.
  useEffect(() => {
    if (!open) return
    function onDown(e: MouseEvent) {
      if (!wrapRef.current?.contains(e.target as Node)) setOpen(false)
    }
    document.addEventListener('mousedown', onDown)
    return () => document.removeEventListener('mousedown', onDown)
  }, [open])

  // Al abrir, el foco va al primer ítem.
  useEffect(() => {
    if (open) wrapRef.current?.querySelectorAll<HTMLElement>('[role="menuitem"]')[active]?.focus()
  }, [open, active])

  if (options.length === 0) return <StatusBadge status={status} label={label} className={className} />

  function close(returnFocus = true) {
    setOpen(false)
    if (returnFocus) triggerRef.current?.focus()
  }

  async function choose(value: string) {
    setOpen(false)
    setError(null)
    setPending(true)
    try {
      await onSelect(value)
    } catch (e) {
      const err = e as { detail?: string; message?: string }
      setError(err.detail ?? err.message ?? 'No se pudo cambiar el estado.')
    } finally {
      setPending(false)
      triggerRef.current?.focus()
    }
  }

  function onMenuKeyDown(e: KeyboardEvent<HTMLDivElement>) {
    if (e.key === 'Escape') {
      e.stopPropagation()
      close()
    } else if (e.key === 'ArrowDown') {
      e.preventDefault()
      setActive(i => (i + 1) % options.length)
    } else if (e.key === 'ArrowUp') {
      e.preventDefault()
      setActive(i => (i - 1 + options.length) % options.length)
    } else if (e.key === 'Home') {
      e.preventDefault()
      setActive(0)
    } else if (e.key === 'End') {
      e.preventDefault()
      setActive(options.length - 1)
    } else if (e.key === 'Tab') {
      close(false)
    }
  }

  return (
    <div ref={wrapRef} className={`relative inline-block ${className}`}>
      <button
        ref={triggerRef}
        type="button"
        aria-haspopup="menu"
        aria-expanded={open}
        aria-controls={open ? menuId : undefined}
        aria-busy={pending || undefined}
        disabled={pending}
        aria-label={`Estado: ${statusText ?? label ?? status}. Ver opciones de estado`}
        onClick={() => { setActive(0); setOpen(o => !o) }}
        className="inline-flex min-h-[28px] items-center gap-1.5 disabled:opacity-60"
      >
        <StatusBadge status={status} label={label} />
        {pending ? (
          <span className="h-3 w-3 animate-spin border-2 border-tc-purple border-t-transparent motion-reduce:animate-none" aria-hidden />
        ) : (
          <svg className="h-3 w-3 text-slate-400" viewBox="0 0 12 12" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden>
            <path d="M2 4.5l4 4 4-4" strokeLinecap="square" />
          </svg>
        )}
      </button>

      {open && (
        <div
          id={menuId}
          role="menu"
          aria-label="Cambiar estado a"
          onKeyDown={onMenuKeyDown}
          className="absolute left-0 top-full z-20 mt-1 min-w-[11rem] border border-tc-border-mid bg-white py-1 shadow-lg"
        >
          {options.map((o, i) => (
            <button
              key={o.value}
              type="button"
              role="menuitem"
              tabIndex={i === active ? 0 : -1}
              onClick={() => void choose(o.value)}
              onMouseEnter={() => setActive(i)}
              className="block w-full px-3 py-2 text-left text-sm text-slate-700 hover:bg-tc-surface focus:bg-tc-surface focus:outline-none"
            >
              {o.label}
            </button>
          ))}
        </div>
      )}

      {error && (
        <p role="alert" className="mt-1 max-w-xs text-xs text-tc-stamp-red">
          {error}
        </p>
      )}
    </div>
  )
}
