import type { ReactNode } from 'react'

export interface LineItemRow {
  id: string
  /** Descripción del renglón. */
  title: string
  /** Texto chico bajo el título (cantidad × precio, IVA, tipo…). */
  detail?: ReactNode
  /** Importe de la línea, a la derecha. */
  amount?: ReactNode
}

/**
 * Renglones de un documento para el celular: una fila por renglón con la descripción que envuelve y el importe a la
 * derecha. La tabla de 5 o 6 columnas no entra en 390 px sin cortar los montos.
 */
export function LineItemsList({ items, label }: { items: LineItemRow[]; label: string }) {
  return (
    <ul aria-label={label} className="divide-y divide-tc-border">
      {items.map(item => (
        <li key={item.id} className="flex items-start justify-between gap-3 px-4 py-3">
          <div className="min-w-0">
            <p className="break-words text-sm font-medium text-tc-text">{item.title}</p>
            {item.detail && <p className="mt-0.5 text-xs text-tc-text-secondary">{item.detail}</p>}
          </div>
          {item.amount != null && <div className="shrink-0 text-right text-sm font-semibold tabular-nums text-tc-purple">{item.amount}</div>}
        </li>
      ))}
    </ul>
  )
}
