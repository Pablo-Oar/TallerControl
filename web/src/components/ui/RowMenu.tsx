import { useEffect, useRef, useState } from 'react'
import { createPortal } from 'react-dom'
import { cn } from '../../lib/utils'

export interface RowMenuItem {
  label: string
  onSelect: () => void
  tone?: 'default' | 'danger' | 'warn'
}

/**
 * Menú «⋯» para las acciones secundarias de una fila: deja a la vista solo la principal.
 * Se dibuja en un portal con posición fija para que no lo recorte el marco de la tabla.
 */
export function RowMenu({ items, label = 'Más acciones' }: { items: RowMenuItem[]; label?: string }) {
  const [pos, setPos] = useState<{ top: number; right: number } | null>(null)
  const btn = useRef<HTMLButtonElement>(null)
  const menu = useRef<HTMLDivElement>(null)

  useEffect(() => {
    if (!pos) return
    const close = () => setPos(null)
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        e.stopPropagation()
        setPos(null)
        btn.current?.focus()
      }
    }
    const onDown = (e: MouseEvent | TouchEvent) => {
      const t = e.target as Node
      if (!menu.current?.contains(t) && !btn.current?.contains(t)) setPos(null)
    }
    document.addEventListener('keydown', onKey, true)
    document.addEventListener('mousedown', onDown)
    document.addEventListener('touchstart', onDown)
    window.addEventListener('scroll', close, true)
    window.addEventListener('resize', close)
    menu.current?.querySelector<HTMLElement>('button')?.focus()
    return () => {
      document.removeEventListener('keydown', onKey, true)
      document.removeEventListener('mousedown', onDown)
      document.removeEventListener('touchstart', onDown)
      window.removeEventListener('scroll', close, true)
      window.removeEventListener('resize', close)
    }
  }, [pos])

  if (items.length === 0) return null

  const toggle = () => {
    if (pos) return setPos(null)
    const r = btn.current!.getBoundingClientRect()
    setPos({ top: r.bottom + 4, right: Math.max(8, window.innerWidth - r.right) })
  }

  return (
    <>
      <button
        ref={btn}
        type="button"
        aria-label={label}
        aria-haspopup="menu"
        aria-expanded={pos !== null}
        onClick={toggle}
        className="flex min-h-11 min-w-11 items-center justify-center rounded-md text-tc-text-secondary transition-colors hover:bg-tc-surface hover:text-tc-text md:min-h-8 md:min-w-8"
      >
        <svg className="h-5 w-5" viewBox="0 0 20 20" fill="currentColor" aria-hidden>
          <circle cx="4" cy="10" r="1.6" /><circle cx="10" cy="10" r="1.6" /><circle cx="16" cy="10" r="1.6" />
        </svg>
      </button>
      {pos &&
        createPortal(
          <div
            ref={menu}
            role="menu"
            style={{ top: pos.top, right: pos.right }}
            className="fixed z-[70] min-w-44 border border-tc-border bg-white py-1 shadow-xl"
          >
            {items.map(it => (
              <button
                key={it.label}
                type="button"
                role="menuitem"
                onClick={() => { setPos(null); it.onSelect() }}
                className={cn(
                  'flex min-h-11 w-full items-center px-4 text-left text-xs font-bold tracking-wider uppercase hover:bg-tc-surface md:min-h-9',
                  it.tone === 'danger' ? 'text-tc-stamp-red' : it.tone === 'warn' ? 'text-amber-700' : 'text-tc-text',
                )}
              >
                {it.label}
              </button>
            ))}
          </div>,
          document.body,
        )}
    </>
  )
}
