import { useMemo, useState, type ReactNode } from 'react'
import { Link } from 'react-router'
import { cn } from '@/lib/utils'
import { Button } from './Button'

/**
 * Tabla de datos del sistema (UI-R2, inspirada en `data-table-8` de Watermelon; código propio con los tokens `tc-*`).
 *
 * - Escritorio: `<table>` real con encabezados ordenables (`aria-sort`), marco y filas redondeados.
 * - Celular (< md): cada fila es una tarjeta (`renderCard`) con la misma lógica que probamos en el teléfono.
 * - Orden: si se pasa `onSortChange` el orden es del **servidor** (listas paginadas: las filas ya vienen ordenadas);
 *   si no, se ordena en el navegador con `sortValue` (listas que caben en una página).
 * - Estados de carga, vacío y error con reintento.
 * - Reordenar columnas arrastrando queda para más adelante.
 */

export type SortDir = 'asc' | 'desc'
export interface SortState {
  key: string
  dir: SortDir
}

export interface DataTableColumn<T> {
  key: string
  header: string
  cell: (row: T) => ReactNode
  align?: 'left' | 'center' | 'right'
  /** Clase de ancho del `th` (p. ej. `w-36`). Las columnas sin ancho se reparten el resto. */
  widthClass?: string
  sortable?: boolean
  /** Valor para ordenar en el navegador (modo cliente). */
  sortValue?: (row: T) => string | number
}

interface DataTableProps<T> {
  /** Nombre accesible de la tabla. */
  label: string
  columns: DataTableColumn<T>[]
  rows: T[]
  getRowId: (row: T) => string
  /** Enlace de la fila: agrega la columna con la flecha. */
  rowHref?: (row: T) => string | null
  rowLabel?: (row: T) => string
  /** Acciones de la fila (Editar, Baja…): última columna en escritorio, fila de botones bajo la tarjeta en celular. */
  actions?: (row: T) => ReactNode
  /** Clases extra de la fila (p. ej. atenuar eliminados). */
  rowClassName?: (row: T) => string | undefined
  /** Tarjeta móvil: talón (número) y cuerpo. */
  cardStub?: (row: T) => ReactNode
  renderCard: (row: T) => ReactNode
  sort?: SortState | null
  onSortChange?: (sort: SortState | null) => void
  loading?: boolean
  error?: boolean
  onRetry?: () => void
  emptyMessage?: string
  className?: string
}

const ALIGN: Record<NonNullable<DataTableColumn<unknown>['align']>, string> = {
  left: 'text-left',
  center: 'text-center',
  right: 'text-right',
}

function Chevron({ dir }: { dir: SortDir | null }) {
  return (
    <svg viewBox="0 0 12 12" className={cn('h-3 w-3 shrink-0', dir ? 'opacity-90' : 'opacity-30')} fill="none" stroke="currentColor" strokeWidth={2} aria-hidden="true">
      {dir === 'desc' ? <path d="M2.5 4.5 6 8l3.5-3.5" /> : dir === 'asc' ? <path d="M2.5 7.5 6 4l3.5 3.5" /> : <path d="M3 5l3-2.5L9 5M3 7l3 2.5L9 7" />}
    </svg>
  )
}

export function DataTable<T>({
  label,
  columns,
  rows,
  getRowId,
  rowHref,
  rowLabel,
  actions,
  rowClassName,
  cardStub,
  renderCard,
  sort: sortProp,
  onSortChange,
  loading = false,
  error = false,
  onRetry,
  emptyMessage = 'No hay datos.',
  className,
}: DataTableProps<T>) {
  const serverSort = !!onSortChange
  const [localSort, setLocalSort] = useState<SortState | null>(null)
  const sort = serverSort ? (sortProp ?? null) : localSort

  function toggle(key: string) {
    // Ciclo: sin orden → ascendente → descendente → sin orden.
    const next: SortState | null =
      sort?.key !== key ? { key, dir: 'asc' } : sort.dir === 'asc' ? { key, dir: 'desc' } : null
    if (serverSort) onSortChange?.(next)
    else setLocalSort(next)
  }

  const shown = useMemo(() => {
    if (serverSort || !sort) return rows
    const col = columns.find(c => c.key === sort.key)
    if (!col?.sortValue) return rows
    const value = col.sortValue
    return [...rows].sort((a, b) => {
      const av = value(a)
      const bv = value(b)
      const cmp = typeof av === 'number' && typeof bv === 'number' ? av - bv : String(av).localeCompare(String(bv), 'es')
      return sort.dir === 'asc' ? cmp : -cmp
    })
  }, [rows, columns, sort, serverSort])

  const frame = cn('rounded-2xl border border-tc-border bg-white p-1.5', className)

  if (error) {
    return (
      <div className={frame}>
        <div role="alert" className="space-y-3 rounded-xl bg-red-50 px-4 py-4 text-sm text-red-800">
          <p>No se pudo cargar la lista.</p>
          {onRetry && (
            <Button size="sm" variant="secondary" onClick={onRetry}>
              Reintentar
            </Button>
          )}
        </div>
      </div>
    )
  }

  if (loading) {
    return (
      <div className={frame} aria-busy="true">
        {[0, 1, 2, 3, 4].map(i => (
          <div key={i} className="mb-1.5 h-14 animate-pulse rounded-xl bg-tc-surface last:mb-0" />
        ))}
      </div>
    )
  }

  if (shown.length === 0) {
    return (
      <div className={frame}>
        <p className="rounded-xl px-4 py-10 text-center text-sm text-tc-text-muted">{emptyMessage}</p>
      </div>
    )
  }

  return (
    <div className={frame}>
      {/* Escritorio */}
      <table className="hidden w-full border-separate border-spacing-0 text-sm md:table">
        <caption className="sr-only">{label}</caption>
        <thead>
          <tr>
            {columns.map(col => {
              const active = sort?.key === col.key ? sort.dir : null
              return (
                <th
                  key={col.key}
                  scope="col"
                  aria-sort={col.sortable ? (active === 'asc' ? 'ascending' : active === 'desc' ? 'descending' : 'none') : undefined}
                  className={cn(
                    'bg-tc-stub-bg px-3 py-2 text-[11px] font-bold tracking-[0.14em] uppercase text-tc-text-muted first:rounded-l-xl last:rounded-r-xl',
                    ALIGN[col.align ?? 'left'],
                    col.widthClass,
                  )}
                >
                  {col.sortable ? (
                    <button
                      type="button"
                      onClick={() => toggle(col.key)}
                      aria-label={`Ordenar por ${col.header}`}
                      className={cn(
                        'inline-flex min-h-[28px] items-center gap-1 rounded-md uppercase tracking-[0.14em] hover:text-tc-text',
                        col.align === 'right' && 'flex-row-reverse',
                        active && 'text-tc-purple',
                      )}
                    >
                      {col.header}
                      <Chevron dir={active} />
                    </button>
                  ) : (
                    col.header
                  )}
                </th>
              )
            })}
            {actions && (
              <th scope="col" className={cn('bg-tc-stub-bg px-3', !rowHref && 'rounded-r-xl')}>
                <span className="sr-only">Acciones</span>
              </th>
            )}
            {rowHref && (
              <th scope="col" className="w-12 rounded-r-xl bg-tc-stub-bg">
                <span className="sr-only">Abrir</span>
              </th>
            )}
          </tr>
        </thead>
        <tbody className="[&>tr:not(:first-child)>td]:border-t [&>tr:not(:first-child)>td]:border-tc-border/70">
          {shown.map(row => (
            <tr key={getRowId(row)} className={cn('group', rowClassName?.(row))}>
              {columns.map(col => (
                <td
                  key={col.key}
                  className={cn(
                    'px-3 py-3 align-middle transition-colors group-hover:bg-tc-surface first:rounded-l-xl',
                    ALIGN[col.align ?? 'left'],
                    !rowHref && !actions && 'last:rounded-r-xl',
                  )}
                >
                  {col.cell(row)}
                </td>
              ))}
              {actions && (
                <td
                  className={cn(
                    'whitespace-nowrap px-2 py-2 text-right align-middle transition-colors group-hover:bg-tc-surface',
                    !rowHref && 'rounded-r-xl',
                  )}
                >
                  <div className="flex items-center justify-end gap-1">{actions(row)}</div>
                </td>
              )}
              {rowHref && (
                <td className="w-12 rounded-r-xl p-0 align-middle transition-colors group-hover:bg-tc-surface">
                  {rowHref(row) && (
                  <Link
                    to={rowHref(row)!}
                    aria-label={rowLabel?.(row) ?? 'Abrir'}
                    className="flex h-11 w-12 items-center justify-center text-tc-navy-text-dim transition-colors hover:text-tc-purple"
                  >
                    <svg className="h-3 w-3" viewBox="0 0 12 12" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden="true">
                      <path d="M2 6h8M6 2l4 4-4 4" strokeLinecap="round" strokeLinejoin="round" />
                    </svg>
                  </Link>
                  )}
                </td>
              )}
            </tr>
          ))}
        </tbody>
      </table>

      {/* Celular: tarjetas */}
      <ul aria-label={label} className="space-y-1.5 md:hidden">
        {shown.map(row => (
          <li key={getRowId(row)} className={cn('flex items-stretch overflow-hidden rounded-xl border border-tc-border bg-white', rowClassName?.(row))}>
            {cardStub && (
              <div className="flex w-14 shrink-0 items-center justify-center border-r border-tc-border bg-tc-stub-bg px-1">
                {cardStub(row)}
              </div>
            )}
            <div className="min-w-0 flex-1 px-3 py-3">
              {renderCard(row)}
              {actions && (
                <div className="mt-2 flex flex-wrap items-center gap-1.5 [&>*]:min-w-[5.5rem] [&>*]:max-w-[12rem] [&>*]:flex-1 [&>*]:justify-center [&>*]:border [&>*]:border-tc-border [&>*]:bg-white [&>*]:text-center">
                  {actions(row)}
                </div>
              )}
            </div>
            {rowHref && rowHref(row) && (
              <Link
                to={rowHref(row)!}
                aria-label={rowLabel?.(row) ?? 'Abrir'}
                className="flex min-h-[44px] w-11 shrink-0 items-center justify-center border-l border-tc-border bg-tc-stub-bg text-tc-navy-text-dim hover:text-tc-purple"
              >
                <svg className="h-3 w-3" viewBox="0 0 12 12" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden="true">
                  <path d="M2 6h8M6 2l4 4-4 4" strokeLinecap="round" strokeLinejoin="round" />
                </svg>
              </Link>
            )}
          </li>
        ))}
      </ul>
    </div>
  )
}

const TONE = {
  default: 'text-tc-navy-text-dim hover:text-tc-purple',
  danger: 'text-tc-stamp-red hover:opacity-70',
  success: 'text-green-700 hover:text-green-900',
  warn: 'text-amber-600 hover:text-amber-800',
  purple: 'text-tc-purple hover:opacity-70',
} as const

/** Acción de texto de una fila: 44 px de alto táctil en celular, compacta en escritorio. */
export function RowAction({
  tone = 'default',
  className,
  ...rest
}: { tone?: keyof typeof TONE } & React.ButtonHTMLAttributes<HTMLButtonElement>) {
  return (
    <button
      type="button"
      className={cn(
        'min-h-11 rounded-md px-3 text-xs font-bold tracking-wider uppercase transition-colors md:min-h-8 md:px-2',
        TONE[tone],
        className,
      )}
      {...rest}
    />
  )
}

/**
 * Orden en el navegador solo cuando la lista entra en una página: si hay más páginas, ordenar lo cargado
 * confunde (la primera fila no sería la menor del total). Las listas largas ordenan en el servidor.
 */
export function sortableWhen<T>(columns: DataTableColumn<T>[], enabled: boolean): DataTableColumn<T>[] {
  return enabled ? columns : columns.map(c => ({ ...c, sortable: false }))
}

/** Igual que `RowAction` pero como enlace (p. ej. descargar un PDF en otra pestaña). */
export function RowActionLink({
  tone = 'default',
  className,
  ...rest
}: { tone?: keyof typeof TONE } & React.AnchorHTMLAttributes<HTMLAnchorElement>) {
  return (
    <a
      className={cn(
        'inline-flex min-h-11 items-center rounded-md px-3 text-xs font-bold tracking-wider uppercase transition-colors md:min-h-8 md:px-2',
        TONE[tone],
        className,
      )}
      {...rest}
    />
  )
}
