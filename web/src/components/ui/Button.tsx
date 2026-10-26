import type { ButtonHTMLAttributes } from 'react'

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  loading?: boolean
  /** ConfirmaciÃ³n breve tras una acciÃ³n exitosa: reemplaza el spinner por un check (mismo tamaÃ±o, sin salto de layout). */
  success?: boolean
  variant?: 'primary' | 'secondary' | 'danger' | 'ghost'
  size?: 'sm' | 'md'
}

const variants = {
  primary:
    'bg-tc-purple text-white hover:bg-tc-purple-mid focus-visible:ring-tc-purple border border-tc-purple',
  secondary:
    'bg-white text-tc-text-secondary border border-tc-border hover:bg-tc-surface focus-visible:ring-tc-navy-text',
  danger:
    'bg-tc-stamp-red text-white hover:bg-red-700 focus-visible:ring-tc-stamp-red border border-tc-stamp-red',
  ghost:
    'bg-transparent text-tc-navy-text border border-tc-navy-mid hover:bg-tc-navy-surface hover:text-white focus-visible:ring-tc-navy-text',
}

const sizes = {
  sm: 'px-3 py-1.5 text-xs max-md:min-h-11',
  md: 'px-4 py-2 text-sm max-md:min-h-11',
}

/** Solo los botones rellenos llevan brillo (â‰¤200 ms, desactivado con reduced-motion). */
const hasSheen = (v: keyof typeof variants) => v === 'primary' || v === 'danger'

export function Button({
  loading,
  success,
  variant = 'primary',
  size = 'md',
  disabled,
  children,
  className = '',
  ...props
}: ButtonProps) {
  return (
    <button
      disabled={disabled || loading}
      aria-busy={loading || undefined}
      className={`group relative inline-flex items-center justify-center gap-2 overflow-hidden font-medium tracking-wide transition-[color,background-color,border-color,transform,translate] duration-150 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-offset-1 enabled:active:translate-y-px motion-reduce:transition-none motion-reduce:active:translate-y-0 disabled:opacity-50 ${variants[variant]} ${sizes[size]} ${className}`}
      {...props}
    >
      {hasSheen(variant) && (
        <span
          aria-hidden
          className="pointer-events-none absolute inset-y-0 left-0 w-1/2 -translate-x-full -skew-x-12 bg-linear-to-r from-transparent via-white/25 to-transparent transition-transform duration-200 ease-out group-enabled:group-hover:translate-x-[300%] motion-reduce:hidden"
        />
      )}
      {loading ? (
        <svg className="h-4 w-4 animate-spin motion-reduce:animate-none" viewBox="0 0 24 24" fill="none" aria-hidden>
          <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
          <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v4a4 4 0 00-4 4H4z" />
        </svg>
      ) : success ? (
        <svg className="tc-check h-4 w-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="3" aria-hidden>
          <path d="M5 12.5l4.5 4.5L19 7.5" strokeLinecap="square" />
        </svg>
      ) : null}
      <span className="relative">{children}</span>
    </button>
  )
}
