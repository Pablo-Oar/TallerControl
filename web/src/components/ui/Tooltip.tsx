import { useEffect, useId, useRef, useState, type ReactNode } from 'react'
import { createPortal } from 'react-dom'
import { cn } from '@/lib/utils'

const HALF = 112 // mitad del ancho máximo del globo (14rem)

/**
 * Globo de ayuda accesible: se abre con el mouse, con el foco del teclado y con el toque (tocar abre,
 * tocar otra vez o Esc cierra). Se dibuja en una capa fija para que ningún recuadro con `overflow` lo recorte
 * y se corre hacia adentro si queda pegado al borde de la pantalla.
 */
export function Tooltip({
  content,
  children,
  focusable = false,
  className,
}: {
  content?: ReactNode
  children: ReactNode
  /** Hace enfocable al contenedor (para contenido que no es interactivo, como un sello). */
  focusable?: boolean
  className?: string
}) {
  const id = useId()
  const ref = useRef<HTMLSpanElement>(null)
  const [pos, setPos] = useState<{ x: number; y: number } | null>(null)
  const openedAt = useRef(0)

  function open() {
    const r = ref.current?.getBoundingClientRect()
    if (!r) return
    const x = Math.min(Math.max(r.left + r.width / 2, HALF + 8), window.innerWidth - HALF - 8)
    openedAt.current = Date.now()
    setPos({ x, y: r.bottom + 6 })
  }
  const close = () => setPos(null)

  useEffect(() => {
    if (!pos) return
    const onKey = (e: KeyboardEvent) => e.key === 'Escape' && setPos(null)
    const onScroll = () => setPos(null)
    document.addEventListener('keydown', onKey)
    window.addEventListener('scroll', onScroll, true)
    return () => {
      document.removeEventListener('keydown', onKey)
      window.removeEventListener('scroll', onScroll, true)
    }
  }, [pos])

  if (!content) return <>{children}</>

  return (
    <span
      ref={ref}
      className={cn('inline-flex align-middle', className)}
      tabIndex={focusable ? 0 : undefined}
      aria-describedby={pos ? id : undefined}
      onMouseEnter={open}
      onMouseLeave={close}
      onFocus={open}
      onBlur={close}
      // En pantallas táctiles el toque dispara primero el "hover" y luego el clic: ese clic no debe cerrarlo.
      onClick={() => (pos && Date.now() - openedAt.current > 500 ? close() : open())}
    >
      {children}
      {pos &&
        createPortal(
          <span
            id={id}
            role="tooltip"
            style={{ left: pos.x, top: pos.y }}
            className="pointer-events-none fixed z-[200] w-max max-w-56 -translate-x-1/2 rounded-lg border border-tc-border bg-white px-2.5 py-1.5 text-center text-xs font-normal leading-snug text-tc-text-secondary shadow-floating"
          >
            {content}
          </span>,
          document.body,
        )}
    </span>
  )
}
