import { useRef } from 'react'

/**
 * Botón "+ Agregar" que va debajo del último renglón de un formulario con filas repetibles:
 * así el renglón nuevo aparece justo arriba y el botón sigue a mano sin volver a subir.
 */
export function AddRowButton({ label, onAdd }: { label: string; onAdd: () => void }) {
  const ref = useRef<HTMLButtonElement>(null)

  return (
    <button
      ref={ref}
      type="button"
      onClick={() => {
        onAdd()
        // Tras agregar la fila, mantiene el botón a la vista (el diálogo scrollea por dentro).
        requestAnimationFrame(() => ref.current?.scrollIntoView?.({ block: 'nearest', behavior: 'smooth' }))
      }}
      className="mt-3 flex min-h-[44px] w-full items-center justify-center rounded-lg border border-dashed border-tc-border-input px-3 text-xs font-bold tracking-wider text-tc-purple uppercase transition-colors hover:bg-tc-surface"
    >
      + {label}
    </button>
  )
}
