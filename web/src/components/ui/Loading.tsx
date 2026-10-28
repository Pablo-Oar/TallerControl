import { cn } from '@/lib/utils'

/**
 * Pantallas de carga: una marca a pantalla completa (arranque, cambio de sesión) y un esqueleto de página
 * para cuando ya se ve el menú. Reemplazan al cuadrado girando sobre fondo blanco.
 */

export function SplashScreen({ className }: { className?: string }) {
  return (
    <div role="status" aria-live="polite" className={cn('flex min-h-screen flex-col items-center justify-center gap-5 bg-tc-navy', className)}>
      <span className="select-none text-lg font-bold tracking-widest text-white uppercase">
        TALLER<span className="text-tc-purple">CONTROL</span>
      </span>
      <span aria-hidden className="block h-0.5 w-28 overflow-hidden bg-white/10">
        <span className="block h-full w-1/3 animate-[tc-indeterminate_1.1s_ease-in-out_infinite] bg-tc-purple motion-reduce:w-full motion-reduce:animate-none" />
      </span>
      <span className="sr-only">Cargando…</span>
    </div>
  )
}

export function PageSkeleton({ className }: { className?: string }) {
  return (
    <div role="status" aria-busy="true" className={cn('w-full space-y-4', className)}>
      <div className="h-6 w-44 animate-pulse rounded bg-tc-surface motion-reduce:animate-none" />
      <div className="space-y-2">
        {[0, 1, 2, 3].map(i => (
          <div key={i} className="h-20 animate-pulse rounded-xl border border-tc-border bg-tc-surface/70 motion-reduce:animate-none" />
        ))}
      </div>
      <span className="sr-only">Cargando…</span>
    </div>
  )
}
