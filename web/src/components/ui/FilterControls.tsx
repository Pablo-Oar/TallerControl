import { forwardRef, type InputHTMLAttributes, type SelectHTMLAttributes } from 'react'
import { cn } from '@/lib/utils'
import { inputCls } from '@/lib/styles'
import { SearchIcon } from './icons'

/** Select compacto para barras de filtros (misma altura táctil, borde y foco que los campos). */
export const FilterSelect = forwardRef<HTMLSelectElement, SelectHTMLAttributes<HTMLSelectElement>>(function FilterSelect(
  { className, ...props },
  ref,
) {
  return <select ref={ref} className={cn(inputCls, 'text-tc-text-secondary', className)} {...props} />
})

/** Campo de búsqueda con lupa. `className` va al contenedor (ancho/flex); el resto, al `<input>`. */
export const SearchInput = forwardRef<HTMLInputElement, InputHTMLAttributes<HTMLInputElement>>(function SearchInput(
  { className, ...props },
  ref,
) {
  return (
    <div className={cn('relative min-w-0', className)}>
      <SearchIcon className="pointer-events-none absolute top-1/2 left-3 h-4 w-4 -translate-y-1/2 text-tc-text-muted" />
      <input ref={ref} type="search" className={cn(inputCls, 'w-full pl-9')} {...props} />
    </div>
  )
})
