import { useEffect, useId, useLayoutEffect, useRef, useState } from 'react'
import { createPortal } from 'react-dom'
import { useQuery } from '@tanstack/react-query'
import { cn } from '@/lib/utils'
import { FormField } from './FormField'

/**
 * Selector con búsqueda asíncrona (combobox): reemplaza los campos donde había que tipear un UUID.
 * Se muestra como un FormField; al elegir, queda un campo de solo lectura con "Cambiar".
 * Teclado: ↑/↓ navegan, Enter elige, Escape cierra.
 */

export interface SearchPickerProps<T> {
  label: string
  /** Id seleccionado ('' = ninguno). */
  value: string
  onChange: (id: string, item: T | null) => void
  /** Identifica la fuente en el caché de TanStack Query (p. ej. "customers"). */
  searchKey: string
  search: (q: string) => Promise<T[]>
  getId: (item: T) => string
  getLabel: (item: T) => string
  getHint?: (item: T) => string | undefined
  /** Para mostrar el nombre de un id inicial (edición) sin haber elegido en esta sesión. */
  fetchLabel?: (id: string) => Promise<string>
  placeholder?: string
  requiredMark?: boolean
  error?: string
  size?: 'md' | 'sm'
  className?: string
  disabled?: boolean
}

export function SearchPicker<T>({
  label,
  value,
  onChange,
  searchKey,
  search,
  getId,
  getLabel,
  getHint,
  fetchLabel,
  placeholder = 'Escribir para buscar…',
  requiredMark,
  error,
  size,
  className,
  disabled,
}: SearchPickerProps<T>) {
  const listId = useId()
  const inputRef = useRef<HTMLInputElement>(null)
  const [q, setQ] = useState('')
  const [debounced, setDebounced] = useState('')
  const [open, setOpen] = useState(false)
  const [active, setActive] = useState(0)
  const [pickedLabel, setPickedLabel] = useState<string | null>(null)
  const anchorRef = useRef<HTMLDivElement>(null)
  // La lista se dibuja en un portal con posición fija: dentro del diálogo la recortaba su área con scroll.
  const [box, setBox] = useState<{ left: number; width: number; top?: number; bottom?: number; maxH: number } | null>(null)

  useLayoutEffect(() => {
    if (!open) return
    const place = () => {
      const r = anchorRef.current?.getBoundingClientRect()
      if (!r) return
      const below = window.innerHeight - r.bottom - 8
      const above = r.top - 8
      const flip = below < 160 && above > below
      setBox(
        flip
          ? { left: r.left, width: r.width, bottom: window.innerHeight - r.top, maxH: Math.min(208, above) }
          : { left: r.left, width: r.width, top: r.bottom + 1, maxH: Math.min(208, Math.max(below, 96)) },
      )
    }
    place()
    window.addEventListener('resize', place)
    window.addEventListener('scroll', place, true)
    return () => {
      window.removeEventListener('resize', place)
      window.removeEventListener('scroll', place, true)
    }
  }, [open])

  useEffect(() => {
    const t = setTimeout(() => setDebounced(q.trim()), 250)
    return () => clearTimeout(t)
  }, [q])

  const { data: results, isFetching } = useQuery({
    queryKey: ['picker', searchKey, debounced],
    queryFn: () => search(debounced),
    enabled: open && !value,
    staleTime: 30_000,
    // conserva la lista anterior mientras llega la nueva: sin parpadeo ni clics perdidos
    placeholderData: prev => prev,
  })

  const { data: fetchedLabel } = useQuery({
    queryKey: ['picker-label', searchKey, value],
    queryFn: () => fetchLabel!(value),
    enabled: !!value && !pickedLabel && !!fetchLabel,
    staleTime: 60_000,
  })

  const items = results ?? []

  function pick(item: T) {
    setPickedLabel(getLabel(item))
    setQ('')
    setOpen(false)
    onChange(getId(item), item)
  }

  function clear() {
    setPickedLabel(null)
    setQ('')
    onChange('', null)
    setTimeout(() => inputRef.current?.focus(), 0)
  }

  function onKeyDown(e: React.KeyboardEvent<HTMLInputElement>) {
    if (e.key === 'ArrowDown') {
      e.preventDefault()
      setOpen(true)
      setActive(i => Math.min(i + 1, Math.max(items.length - 1, 0)))
    } else if (e.key === 'ArrowUp') {
      e.preventDefault()
      setActive(i => Math.max(i - 1, 0))
    } else if (e.key === 'Enter' && open && items[active]) {
      e.preventDefault()
      pick(items[active])
    } else if (e.key === 'Escape' && open) {
      e.stopPropagation()
      setOpen(false)
    }
  }

  if (value) {
    return (
      <div className={cn('relative', className)}>
        <FormField
          label={label}
          requiredMark={requiredMark}
          size={size}
          readOnly
          tabIndex={-1}
          value={pickedLabel ?? fetchedLabel ?? '…'}
          inputClassName="pr-24"
        />
        {!disabled && (
          <button
            type="button"
            onClick={clear}
            className="absolute right-2 top-1/2 -translate-y-1/2 px-2 py-2 text-xs font-bold uppercase tracking-wider text-tc-text-secondary hover:text-tc-stamp-red"
          >
            Cambiar
          </button>
        )}
      </div>
    )
  }

  return (
    <div ref={anchorRef} className={cn('relative', className)}>
      <FormField
        ref={inputRef}
        label={label}
        requiredMark={requiredMark}
        size={size}
        type="text"
        role="combobox"
        aria-expanded={open}
        aria-controls={listId}
        aria-autocomplete="list"
        aria-activedescendant={open && items[active] ? `${listId}-${active}` : undefined}
        autoComplete="off"
        disabled={disabled}
        placeholder={placeholder}
        error={error}
        value={q}
        onChange={e => {
          setQ(e.target.value)
          setActive(0)
          setOpen(true)
        }}
        // Se abre al tocar/escribir/↓, no por foco: el diálogo enfoca el primer campo solo y la lista taparía el resto.
        onClick={() => setOpen(true)}
        onBlur={() => setOpen(false)}
        onKeyDown={onKeyDown}
      />
      {open && box && createPortal(
        <ul
          id={listId}
          role="listbox"
          aria-label={label}
          // mousedown (no click): se elige antes de que el blur cierre la lista
          onMouseDown={e => e.preventDefault()}
          style={{ left: box.left, width: box.width, top: box.top, bottom: box.bottom, maxHeight: box.maxH }}
          className="fixed z-[70] overflow-y-auto border border-tc-border-mid bg-white shadow-lg"
        >
          {isFetching && items.length === 0 && <li className="px-3 py-2 text-sm text-tc-text-muted">Buscando…</li>}
          {!isFetching && items.length === 0 && <li className="px-3 py-2 text-sm text-tc-text-muted">Sin resultados</li>}
          {items.map((item, i) => (
            <li
              key={getId(item)}
              id={`${listId}-${i}`}
              role="option"
              aria-selected={i === active}
              onMouseEnter={() => setActive(i)}
              onClick={() => pick(item)}
              className={cn('cursor-pointer px-3 py-2 text-sm', i === active ? 'bg-tc-surface' : 'bg-white')}
            >
              <span className="font-medium text-tc-text">{getLabel(item)}</span>
              {getHint?.(item) && <span className="ml-2 text-xs text-tc-text-muted">{getHint(item)}</span>}
            </li>
          ))}
        </ul>,
        document.body,
      )}
    </div>
  )
}
