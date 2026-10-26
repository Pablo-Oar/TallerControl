import { useEffect, useId, useRef, useSyncExternalStore, type ReactNode } from 'react'
import { createPortal } from 'react-dom'

/**
 * Diálogo del sistema (UI-R2 F2, D-24/D-25). Conserva la estética "Jet-Age Ticket Wallet" (cabecera navy, línea
 * perforada) y su API (`title`, `onClose`, `children`, `className`); por dentro resuelve lo que faltaba:
 *  - Escape cierra solo el diálogo superior de la pila.
 *  - El foco entra al diálogo, queda atrapado (Tab / Shift+Tab) y vuelve al elemento que lo abrió.
 *  - El fondo queda inerte (`inert`) y no hace scroll mientras haya algún diálogo abierto.
 *  - `role="dialog"`, `aria-modal` y `aria-labelledby` apuntando al título.
 *  - Por debajo de 640 px se presenta como hoja inferior que ocupa el alto disponible.
 * Es propio (no se vendorea `dialog-stack` de Watermelon: exige motion, lucide y hugeicons).
 */

// ── Pila de diálogos abiertos (módulo): permite saber cuál es el superior y bloquear el fondo una sola vez ──
let stack: string[] = []
const listeners = new Set<() => void>()
let previousBodyStyle: { overflow: string; paddingRight: string } | null = null

function applyBackgroundLock(open: boolean) {
  const root = document.getElementById('root')
  if (open && !previousBodyStyle) {
    const { overflow, paddingRight } = document.body.style
    previousBodyStyle = { overflow, paddingRight }
    const scrollbar = window.innerWidth - document.documentElement.clientWidth
    document.body.style.overflow = 'hidden'
    if (scrollbar > 0) document.body.style.paddingRight = `${scrollbar}px` // evita el salto de layout
    root?.setAttribute('inert', '')
  } else if (!open && previousBodyStyle) {
    document.body.style.overflow = previousBodyStyle.overflow
    document.body.style.paddingRight = previousBodyStyle.paddingRight
    previousBodyStyle = null
    root?.removeAttribute('inert')
  }
}

function setStack(next: string[]) {
  stack = next
  applyBackgroundLock(stack.length > 0)
  listeners.forEach(l => l())
}

const subscribe = (l: () => void) => {
  listeners.add(l)
  return () => listeners.delete(l)
}
const getSnapshot = () => stack

const FOCUSABLE =
  'a[href], button:not([disabled]), input:not([disabled]):not([type="hidden"]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'

/** Visible para el teclado: sin `display:none`, `visibility:hidden` ni `hidden` en el propio elemento o en un ancestro directo. */
function isVisible(el: HTMLElement) {
  const style = getComputedStyle(el)
  return style.display !== 'none' && style.visibility !== 'hidden' && !el.closest('[hidden]')
}

interface ModalProps {
  title: string
  onClose: () => void
  children: ReactNode
  /** Extra classes on the panel (e.g. max-w-lg) */
  className?: string
}

export function Modal({ title, onClose, children, className = 'max-w-lg' }: ModalProps) {
  const id = useId()
  const titleId = `${id}-title`
  const panelRef = useRef<HTMLDivElement>(null)
  const pressedOnBackdrop = useRef(false)
  const openStack = useSyncExternalStore(subscribe, getSnapshot)
  // Aún no registrado (primer render) cuenta como superior: así el foco inicial no cae en un nodo inerte.
  const isTop = !openStack.includes(id) || openStack[openStack.length - 1] === id

  // Registro en la pila y manejo del foco (entrar al abrir, volver al cerrar).
  useEffect(() => {
    const opener = document.activeElement instanceof HTMLElement ? document.activeElement : null
    setStack([...stack, id])

    const panel = panelRef.current
    // En el celular no se enfoca el primer campo: abriría el teclado al instante y taparía el diálogo (el foco va al panel).
    const touch = typeof window.matchMedia === 'function' && window.matchMedia('(pointer: coarse)').matches
    const first = touch
      ? panel?.querySelector<HTMLElement>('[data-autofocus]')
      : (panel?.querySelector<HTMLElement>('[data-autofocus], input, select, textarea') ?? panel?.querySelector<HTMLElement>(FOCUSABLE))
    ;(first ?? panel)?.focus()

    return () => {
      setStack(stack.filter(x => x !== id))
      if (opener?.isConnected) opener.focus()
    }
  }, [id])

  // Si el foco se pierde (p. ej. se desmonta el botón enfocado y cae al body), Escape y Tab siguen valiendo para el
  // diálogo superior. Con el foco dentro del panel los resuelve onKeyDown (React), sin duplicar.
  useEffect(() => {
    if (!isTop) return
    function onDocumentKeyDown(e: KeyboardEvent) {
      const panel = panelRef.current
      if (!panel || panel.contains(document.activeElement)) return
      if (e.key === 'Escape') {
        onClose()
      } else if (e.key === 'Tab') {
        e.preventDefault()
        const items = Array.from(panel.querySelectorAll<HTMLElement>(FOCUSABLE)).filter(isVisible)
        ;(e.shiftKey ? items[items.length - 1] : items[0] ?? panel)?.focus()
      }
    }
    document.addEventListener('keydown', onDocumentKeyDown)
    return () => document.removeEventListener('keydown', onDocumentKeyDown)
  }, [isTop, onClose])

  function onKeyDown(e: React.KeyboardEvent<HTMLDivElement>) {
    if (!isTop) return
    if (e.key === 'Escape') {
      e.stopPropagation()
      onClose()
      return
    }
    if (e.key !== 'Tab') return
    const items = Array.from(panelRef.current?.querySelectorAll<HTMLElement>(FOCUSABLE) ?? []).filter(isVisible)
    if (items.length === 0) {
      e.preventDefault()
      panelRef.current?.focus()
      return
    }
    const firstEl = items[0]
    const lastEl = items[items.length - 1]
    const active = document.activeElement
    if (e.shiftKey && (active === firstEl || active === panelRef.current)) {
      e.preventDefault()
      lastEl.focus()
    } else if (!e.shiftKey && active === lastEl) {
      e.preventDefault()
      firstEl.focus()
    }
  }

  return createPortal(
    <div
      className="fixed inset-0 z-50 flex items-end justify-center overflow-y-auto bg-black/60 sm:items-start sm:p-4 sm:pt-16"
      onMouseDown={e => { pressedOnBackdrop.current = e.target === e.currentTarget }}
      onClick={e => { if (e.target === e.currentTarget && pressedOnBackdrop.current) onClose() }}
      onKeyDown={onKeyDown}
    >
      <div
        ref={panelRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        tabIndex={-1}
        // Un diálogo que no es el superior queda inerte: el foco y los clics solo llegan al de arriba.
        inert={!isTop}
        className={`tc-modal-panel relative flex max-h-[92dvh] w-full flex-col bg-white shadow-2xl outline-none max-sm:max-w-none sm:max-h-[calc(100dvh-5rem)] ${className}`}
      >
        {/* Header */}
        <div className="flex flex-none items-center justify-between bg-tc-navy px-5 py-3">
          <h2 id={titleId} className="text-sm font-bold tracking-[0.1em] uppercase text-white">{title}</h2>
          <button
            type="button"
            onClick={onClose}
            className="flex h-11 w-11 items-center justify-center text-tc-navy-text hover:text-white transition-colors -mr-2"
            aria-label="Cerrar"
          >
            <svg className="h-4 w-4" viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden>
              <path d="M3 3l10 10M13 3L3 13" strokeLinecap="round" />
            </svg>
          </button>
        </div>
        {/* Perforation line */}
        <div className="h-px w-full flex-none bg-tc-perf" />
        {/* Body: en móvil (hoja) hace scroll propio; en escritorio crece con el contenido */}
        <div className="min-h-0 flex-1 overflow-y-auto p-5">
          {children}
        </div>
      </div>
    </div>,
    document.body,
  )
}
