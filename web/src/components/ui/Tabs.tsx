import { useCallback, useLayoutEffect, useRef, type KeyboardEvent, type ReactNode } from 'react'
import { NavLink, useLocation } from 'react-router'
import { cn } from '@/lib/utils'

/**
 * Pestañas del sistema (UI-R2 F3, D-24/D-25). Dos modos con el mismo aspecto (subrayado violeta que se desliza):
 *  - `Tabs` (controlado): estado local. `role=tablist/tab/tabpanel`, flechas ←/→ y Home/End, tabulación "rodante"
 *    (solo la pestaña activa entra en el orden de Tab). Se acompaña de `TabPanel`.
 *  - `TabNav` (navegación): cada pestaña es un `NavLink` a una ruta; la activa lleva `aria-current="page"`.
 * Propio, no se vendorea `fluid-tabs` (exige react-icons); el efecto "fluido" es un indicador con transición CSS.
 */

const BAR = 'relative flex overflow-x-auto border-b border-tc-border [scrollbar-width:none] [&::-webkit-scrollbar]:hidden'
const TAB =
  'flex min-h-[44px] shrink-0 items-center whitespace-nowrap px-3 text-xs sm:px-4 font-bold uppercase tracking-wider transition-colors'
const ACTIVE = 'text-tc-purple'
const IDLE = 'text-tc-text-secondary hover:text-tc-text'

/** Indicador deslizante: mide la pestaña activa y lo posiciona; sin transición con prefers-reduced-motion. */
function useIndicator(activeKey: string) {
  const barRef = useRef<HTMLDivElement>(null)
  const indicatorRef = useRef<HTMLSpanElement>(null)

  const update = useCallback(() => {
    const bar = barRef.current
    const indicator = indicatorRef.current
    const active = bar?.querySelector<HTMLElement>('[aria-selected="true"], [aria-current="page"]')
    if (!bar || !indicator || !active) return
    indicator.style.width = `${active.offsetWidth}px`
    indicator.style.transform = `translateX(${active.offsetLeft}px)`
    indicator.style.opacity = '1'
    // En móvil la pestaña activa puede quedar fuera del ancho visible: se la trae a la vista.
    active.scrollIntoView?.({ inline: 'nearest', block: 'nearest' })
  }, [])

  useLayoutEffect(() => {
    update()
    const bar = barRef.current
    if (!bar || typeof ResizeObserver === 'undefined') return
    const ro = new ResizeObserver(update)
    ro.observe(bar)
    return () => ro.disconnect()
  }, [activeKey, update])

  const indicator = (
    <span
      ref={indicatorRef}
      aria-hidden
      className="pointer-events-none absolute bottom-0 left-0 h-0.5 bg-tc-purple opacity-0 transition-[transform,width] duration-200 ease-out motion-reduce:transition-none"
    />
  )
  return { barRef, indicator }
}

export interface TabItem<T extends string = string> {
  id: T
  label: string
}

interface TabsProps<T extends string> {
  items: TabItem<T>[]
  value: T
  onChange: (id: T) => void
  /** Nombre accesible de la lista de pestañas. */
  label: string
  /** Prefijo para relacionar cada pestaña con su `TabPanel` (ids únicos por pantalla). */
  idPrefix: string
  className?: string
}

const tabId = (idPrefix: string, id: string) => `${idPrefix}-tab-${id}`
const panelId = (idPrefix: string, id: string) => `${idPrefix}-panel-${id}`

export function Tabs<T extends string>({ items, value, onChange, label, idPrefix, className }: TabsProps<T>) {
  const { barRef, indicator } = useIndicator(value)

  function onKeyDown(e: KeyboardEvent<HTMLDivElement>) {
    const index = items.findIndex(i => i.id === value)
    let next = -1
    if (e.key === 'ArrowRight') next = (index + 1) % items.length
    else if (e.key === 'ArrowLeft') next = (index - 1 + items.length) % items.length
    else if (e.key === 'Home') next = 0
    else if (e.key === 'End') next = items.length - 1
    if (next < 0) return
    e.preventDefault()
    onChange(items[next].id)
    // la activación sigue al foco: se enfoca la pestaña nueva en el siguiente frame (ya renderizada como activa)
    requestAnimationFrame(() => document.getElementById(tabId(idPrefix, items[next].id))?.focus())
  }

  return (
    <div ref={barRef} role="tablist" aria-label={label} className={cn(BAR, className)} onKeyDown={onKeyDown}>
      {items.map(item => {
        const selected = item.id === value
        return (
          <button
            key={item.id}
            id={tabId(idPrefix, item.id)}
            type="button"
            role="tab"
            aria-selected={selected}
            aria-controls={panelId(idPrefix, item.id)}
            tabIndex={selected ? 0 : -1}
            onClick={() => onChange(item.id)}
            className={cn(TAB, selected ? ACTIVE : IDLE)}
          >
            {item.label}
          </button>
        )
      })}
      {indicator}
    </div>
  )
}

/** Contenido de una pestaña: solo se renderiza la activa. */
export function TabPanel({ idPrefix, tabId: id, active, children }: { idPrefix: string; tabId: string; active: boolean; children: ReactNode }) {
  if (!active) return null
  return (
    <div role="tabpanel" id={panelId(idPrefix, id)} aria-labelledby={tabId(idPrefix, id)} tabIndex={0} className="outline-none">
      {children}
    </div>
  )
}

export interface TabNavItem {
  to: string
  label: string
  /** Coincidir solo con la ruta exacta (para la pestaña "raíz"). */
  end?: boolean
}

/** Pestañas que navegan: cada una es un enlace; la activa lleva `aria-current="page"`. */
export function TabNav({ items, label, className }: { items: TabNavItem[]; label: string; className?: string }) {
  const { pathname } = useLocation()
  const { barRef, indicator } = useIndicator(pathname)
  return (
    <nav ref={barRef} aria-label={label} className={cn(BAR, className)}>
      {items.map(item => (
        <NavLink
          key={item.to}
          to={item.to}
          end={item.end}
          className={({ isActive }) => cn(TAB, isActive ? ACTIVE : IDLE)}
        >
          {item.label}
        </NavLink>
      ))}
      {indicator}
    </nav>
  )
}
