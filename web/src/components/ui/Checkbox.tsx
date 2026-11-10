import { forwardRef, type InputHTMLAttributes } from 'react'
import { cn } from '@/lib/utils'

type Props = Omit<InputHTMLAttributes<HTMLInputElement>, 'type'>

const base = 'h-4 w-4 shrink-0 cursor-pointer accent-tc-purple disabled:cursor-not-allowed disabled:opacity-50'

/** Casilla con el estilo común (violeta, 16 px). Compatible con `register()` de react-hook-form. */
export const Checkbox = forwardRef<HTMLInputElement, Props>(function Checkbox({ className, ...props }, ref) {
  return <input ref={ref} type="checkbox" className={cn(base, className)} {...props} />
})

/** Botón de opción con el mismo estilo que `Checkbox`. */
export const Radio = forwardRef<HTMLInputElement, Props>(function Radio({ className, ...props }, ref) {
  return <input ref={ref} type="radio" className={cn(base, className)} {...props} />
})
