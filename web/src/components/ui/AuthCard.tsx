import { forwardRef, useId, useState, type InputHTMLAttributes, type ReactNode } from 'react'
import { cn } from '@/lib/utils'
import { PublicNav } from './PublicNav'
import { EyeIcon, EyeOffIcon } from './icons'

/**
 * Tarjeta de autenticación (UI-R2, bloques `auth-01`/`auth-02` de Watermelon): bisel exterior con la tarjeta blanca
 * adentro, título centrado, campos con ícono y pie fuera de la tarjeta. Código propio con los tokens `tc-*`.
 * Las pantallas de acceso llevan bordes redondeados con radios explícitos (el resto de la app mantiene D-26,
 * `--radius: 0`); sin proveedores sociales hasta que exista el login con Google (E18).
 */
export function AuthCard({
  title,
  subtitle,
  footer,
  children,
  className,
}: {
  title: string
  subtitle?: string
  footer?: ReactNode
  children: ReactNode
  className?: string
}) {
  return (
    <div
      className="flex min-h-dvh flex-col bg-tc-navy"
      style={{
        backgroundImage:
          'linear-gradient(rgba(148,163,184,0.08) 1px, transparent 1px), linear-gradient(90deg, rgba(148,163,184,0.08) 1px, transparent 1px)',
        backgroundSize: '44px 44px',
      }}
    >
     <PublicNav />
     <div className="flex flex-1 items-center justify-center px-4 py-10">
      <div className={cn('w-full max-w-sm rounded-[28px] border border-tc-navy-mid bg-tc-stub-bg p-2', className)}>
        <div className="rounded-[22px] border border-tc-border bg-white px-5 py-7 shadow-sm sm:px-7">
          <div className="mb-6 text-center">
            <p className="mb-3 text-[11px] font-bold tracking-[0.14em] uppercase text-tc-text-muted">TallerControl</p>
            <h1 className="text-xl font-extrabold tracking-tight text-tc-text sm:text-2xl">{title}</h1>
            {subtitle && <p className="mt-1.5 text-sm leading-relaxed text-tc-text-secondary">{subtitle}</p>}
          </div>
          {children}
        </div>
        {footer && <div className="px-2 pt-4 pb-2 text-center text-sm text-tc-text-secondary">{footer}</div>}
      </div>
     </div>
    </div>
  )
}

interface AuthInputProps extends Omit<InputHTMLAttributes<HTMLInputElement>, 'size'> {
  label: string
  /** Ícono a la izquierda (de `icons.tsx`). */
  icon: ReactNode
  error?: string
  hint?: string
}

/**
 * Campo con ícono y etiqueta flotante: la etiqueta vive dentro del campo y, al enfocarlo o escribir, sube y se
 * apoya sobre el borde (el `placeholder` solo se ve con el foco, y solo si aporta algo distinto de la etiqueta). Con `type="password"` agrega mostrar/ocultar.
 */
export const AuthInput = forwardRef<HTMLInputElement, AuthInputProps>(function AuthInput(
  { label, icon, error, hint, type = 'text', className, id: idProp, placeholder, ...rest },
  ref,
) {
  const autoId = useId()
  const id = idProp ?? autoId
  const descId = `${id}-desc`
  const [shown, setShown] = useState(false)
  const isPassword = type === 'password'

  return (
    <div className={className}>
      <div className="relative">
        <span className="pointer-events-none absolute top-1/2 left-3 -translate-y-1/2 text-tc-text-muted">{icon}</span>
        <input
          ref={ref}
          id={id}
          data-auth-field=""
          type={isPassword && shown ? 'text' : type}
          // El espacio fuerza `:placeholder-shown` (campo vacío) cuando no hay placeholder propio.
          placeholder={placeholder ?? ' '}
          aria-invalid={error ? true : undefined}
          aria-describedby={error || hint ? descId : undefined}
          className={cn(
            'peer block h-12 w-full rounded-xl border bg-white pl-10 text-sm text-tc-text outline-none transition-colors',
            'placeholder:text-transparent focus:placeholder:text-tc-text-muted',
            isPassword ? 'pr-11' : 'pr-3',
            error
              ? 'border-tc-stamp-red'
              : 'border-tc-border-mid hover:border-tc-text-muted focus:border-tc-purple',
          )}
          {...rest}
        />
        <label
          htmlFor={id}
          className={cn(
            'pointer-events-none absolute left-10 top-1/2 -translate-y-1/2 bg-white px-1 text-sm text-tc-text-muted transition-all duration-300 ease-out',
            // Al enfocar o escribir, la etiqueta sube lentamente y se apoya sobre el borde superior del campo.
            'peer-focus:top-0 peer-focus:left-3 peer-focus:text-xs peer-focus:font-semibold',
            'peer-[:not(:placeholder-shown)]:top-0 peer-[:not(:placeholder-shown)]:left-3 peer-[:not(:placeholder-shown)]:text-xs peer-[:not(:placeholder-shown)]:font-semibold',
            error ? 'text-tc-stamp-red peer-focus:text-tc-stamp-red' : 'peer-focus:text-tc-purple',
          )}
        >
          {label}
        </label>
        {isPassword && (
          <button
            type="button"
            onClick={() => setShown(v => !v)}
            aria-label={shown ? 'Ocultar contraseña' : 'Mostrar contraseña'}
            aria-pressed={shown}
            className="absolute top-1/2 right-0 flex h-11 w-11 -translate-y-1/2 items-center justify-center rounded-xl text-tc-text-muted hover:text-tc-text"
          >
            {shown ? <EyeOffIcon /> : <EyeIcon />}
          </button>
        )}
      </div>
      {(error || hint) && (
        <p id={descId} className={cn('mt-1 text-xs', error ? 'text-tc-stamp-red' : 'text-tc-text-muted')}>
          {error ?? hint}
        </p>
      )}
    </div>
  )
})

/** Aviso de error del servidor dentro de una pantalla de acceso (mismo radio que los campos). */
export function AuthAlert({ children }: { children: ReactNode }) {
  return (
    <p role="alert" className="rounded-xl border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
      {children}
    </p>
  )
}
