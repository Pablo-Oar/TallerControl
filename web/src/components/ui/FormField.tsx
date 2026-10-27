import {
  forwardRef,
  useId,
  type InputHTMLAttributes,
  type ReactNode,
  type SelectHTMLAttributes,
  type TextareaHTMLAttributes,
} from 'react'
import { cn } from '@/lib/utils'

/**
 * Campo de formulario con etiqueta flotante (UI-R2 F1, D-24/D-25).
 *
 * Inspirado en `floating-input` de Watermelon, pero sin su estado interno: la etiqueta sube por CSS
 * (`:focus` / `:not(:placeholder-shown)`), así que refleja valores iniciales, `reset()` y `setValue()` de
 * React Hook Form. La etiqueta siempre está asociada (`htmlFor`/`id`) y hay una sola señal de foco
 * (el anillo global de `index.css`). Esquinas rectas por D-26.
 */

type Size = 'md' | 'sm'

interface BaseProps {
  label: string
  /** Mensaje de error; marca el campo como inválido y se anuncia por aria-describedby. */
  error?: string
  hint?: string
  /** Solo marca visual (asterisco). La validación sigue en zod, no en `required` nativo. */
  requiredMark?: boolean
  size?: Size
  /** Clases del contenedor (ancho, grid-span…). */
  className?: string
  /** Clases extra del control (p. ej. `font-mono uppercase`). */
  inputClassName?: string
}

const ALWAYS_RAISED = new Set(['date', 'time', 'datetime-local', 'month', 'week', 'file', 'color'])

const controlBase =
  'peer block w-full border bg-white text-sm text-tc-text outline-none transition-colors ' +
  'placeholder:text-transparent focus:placeholder:text-tc-text-muted ' +
  'disabled:cursor-not-allowed disabled:bg-tc-surface disabled:text-tc-text-muted'

const controlSize: Record<Size, string> = {
  md: 'px-3 pt-5 pb-1.5',
  sm: 'px-3 pt-4 pb-1',
}

function borderCls(error?: string) {
  return error ? 'border-tc-stamp-red' : 'border-tc-border-mid'
}

function labelCls(raised: boolean, size: Size, multiline: boolean) {
  const rest = multiline
    ? 'top-3 text-sm'
    : 'top-1/2 -translate-y-1/2 text-sm'
  const up = size === 'sm' ? 'top-0.5 translate-y-0 text-[11px]' : 'top-1.5 translate-y-0 text-[11px]'
  return cn(
    'pointer-events-none absolute left-3 origin-left truncate text-tc-text-muted transition-all duration-150 motion-reduce:transition-none',
    'max-w-[calc(100%-1.5rem)] peer-focus:text-tc-purple',
    raised
      ? cn(up, 'font-medium')
      : cn(rest, 'peer-focus:translate-y-0 peer-focus:font-medium', size === 'sm' ? 'peer-focus:top-0.5 peer-focus:text-[11px]' : 'peer-focus:top-1.5 peer-focus:text-[11px]'),
    !raised &&
      (size === 'sm'
        ? 'peer-not-placeholder-shown:top-0.5 peer-not-placeholder-shown:translate-y-0 peer-not-placeholder-shown:text-[11px] peer-not-placeholder-shown:font-medium'
        : 'peer-not-placeholder-shown:top-1.5 peer-not-placeholder-shown:translate-y-0 peer-not-placeholder-shown:text-[11px] peer-not-placeholder-shown:font-medium'),
  )
}

interface ShellProps extends BaseProps {
  id: string
  raised: boolean
  multiline?: boolean
  children: ReactNode
}

function Shell({ id, label, error, hint, requiredMark, size = 'md', className, raised, multiline = false, children }: ShellProps) {
  return (
    <div className={className}>
      <div className="relative">
        {children}
        {multiline && <span aria-hidden className="pointer-events-none absolute inset-x-px top-px h-5 bg-white peer-disabled:bg-tc-surface" />}
        <label htmlFor={id} className={labelCls(raised, size, multiline)}>
          {label}
          {requiredMark && <span aria-hidden> *</span>}
        </label>
      </div>
      {error ? (
        <p id={`${id}-msg`} role="alert" className="mt-1 text-xs text-tc-stamp-red">
          {error}
        </p>
      ) : hint ? (
        <p id={`${id}-msg`} className="mt-1 text-xs text-tc-text-secondary">
          {hint}
        </p>
      ) : null}
    </div>
  )
}

function describedBy(id: string, error?: string, hint?: string) {
  return error || hint ? `${id}-msg` : undefined
}

export interface FormFieldProps extends BaseProps, Omit<InputHTMLAttributes<HTMLInputElement>, 'size' | 'className'> {}

export const FormField = forwardRef<HTMLInputElement, FormFieldProps>(function FormField(
  { label, error, hint, requiredMark, size = 'md', className, inputClassName, id, type = 'text', placeholder, ...props },
  ref,
) {
  const autoId = useId()
  const fieldId = id ?? autoId
  return (
    <Shell id={fieldId} {...{ label, error, hint, requiredMark, size, className }} raised={ALWAYS_RAISED.has(type)}>
      <input
        data-field=""
        ref={ref}
        id={fieldId}
        type={type}
        placeholder={placeholder ?? ' '}
        aria-invalid={error ? true : undefined}
        aria-describedby={describedBy(fieldId, error, hint)}
        className={cn(controlBase, controlSize[size], borderCls(error), inputClassName)}
        {...props}
      />
    </Shell>
  )
})

export interface FormTextareaProps extends BaseProps, Omit<TextareaHTMLAttributes<HTMLTextAreaElement>, 'className'> {}

export const FormTextarea = forwardRef<HTMLTextAreaElement, FormTextareaProps>(function FormTextarea(
  { label, error, hint, requiredMark, size = 'md', className, inputClassName, id, placeholder, rows = 3, ...props },
  ref,
) {
  const autoId = useId()
  const fieldId = id ?? autoId
  return (
    <Shell id={fieldId} {...{ label, error, hint, requiredMark, size, className }} raised={false} multiline>
      <textarea
        data-field=""
        ref={ref}
        id={fieldId}
        rows={rows}
        placeholder={placeholder ?? ' '}
        aria-invalid={error ? true : undefined}
        aria-describedby={describedBy(fieldId, error, hint)}
        className={cn(controlBase, controlSize[size], borderCls(error), 'min-h-[5.5rem] max-h-48 resize-none [field-sizing:content]', inputClassName)}
        {...props}
      />
    </Shell>
  )
})

export interface FormSelectProps extends BaseProps, Omit<SelectHTMLAttributes<HTMLSelectElement>, 'size' | 'className'> {}

/** El select siempre tiene valor: la etiqueta queda arriba, igual que un campo con contenido. */
export const FormSelect = forwardRef<HTMLSelectElement, FormSelectProps>(function FormSelect(
  { label, error, hint, requiredMark, size = 'md', className, inputClassName, id, children, ...props },
  ref,
) {
  const autoId = useId()
  const fieldId = id ?? autoId
  return (
    <Shell id={fieldId} {...{ label, error, hint, requiredMark, size, className }} raised>
      <select
        data-field=""
        ref={ref}
        id={fieldId}
        aria-invalid={error ? true : undefined}
        aria-describedby={describedBy(fieldId, error, hint)}
        className={cn(controlBase, controlSize[size], borderCls(error), inputClassName)}
        {...props}
      >
        {children}
      </select>
    </Shell>
  )
})
