import type { ReactNode } from 'react'
import { cn } from '@/lib/utils'

/** Tarjeta base: borde de 1 px, esquinas de 14 px, sin sombra en reposo. */
export function Card({ children, className }: { children: ReactNode; className?: string }) {
  return (
    <section className={cn('overflow-hidden rounded-xl border border-tc-border bg-white', className)}>{children}</section>
  )
}

/** Franja de título de una tarjeta (talón gris con el título en mayúsculas chicas). */
export function CardHeader({ title, actions, className }: { title: ReactNode; actions?: ReactNode; className?: string }) {
  return (
    <div className={cn('flex items-center justify-between gap-3 border-b border-tc-border bg-tc-stub-bg px-4 py-2', className)}>
      <h2 className="text-[11px] font-bold tracking-[0.14em] text-tc-text-secondary uppercase">{title}</h2>
      {actions}
    </div>
  )
}
