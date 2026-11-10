import type { ReactNode } from 'react'
import { cn } from '@/lib/utils'

export type BadgeTone = 'success' | 'warning' | 'danger' | 'info' | 'neutral' | 'brand'

const TONES: Record<BadgeTone, string> = {
  success: 'border-tc-success-border bg-tc-success-bg text-tc-success-text',
  warning: 'border-tc-warning-border bg-tc-warning-bg text-tc-warning-text',
  danger: 'border-tc-danger-border bg-tc-danger-bg text-tc-danger-text',
  info: 'border-tc-info-border bg-tc-info-bg text-tc-info-text',
  neutral: 'border-tc-border bg-tc-surface text-tc-text-secondary',
  brand: 'border-tc-purple bg-tc-purple-light text-tc-purple-mid',
}

/** Etiqueta corta de estado o categoría (píldora). Para el estado de una orden/presupuesto usar `StatusBadge`. */
export function Badge({
  tone = 'neutral',
  children,
  className,
}: {
  tone?: BadgeTone
  children: ReactNode
  className?: string
}) {
  return (
    <span
      className={cn(
        'inline-flex items-center gap-1 rounded-full border px-2 py-0.5 text-[11px] font-bold tracking-wider uppercase',
        TONES[tone],
        className,
      )}
    >
      {children}
    </span>
  )
}
