import type { ButtonHTMLAttributes } from 'react'

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  loading?: boolean
  variant?: 'primary' | 'secondary' | 'danger' | 'ghost'
  size?: 'sm' | 'md'
}

const variants = {
  primary:
    'bg-tc-purple text-white hover:bg-tc-purple-mid focus-visible:ring-tc-purple border border-tc-purple',
  secondary:
    'bg-white text-slate-700 border border-tc-border hover:bg-tc-surface focus-visible:ring-tc-navy-text',
  danger:
    'bg-tc-stamp-red text-white hover:bg-red-700 focus-visible:ring-tc-stamp-red border border-tc-stamp-red',
  ghost:
    'bg-transparent text-tc-navy-text border border-tc-navy-mid hover:bg-tc-navy-surface hover:text-white focus-visible:ring-tc-navy-text',
}

const sizes = {
  sm: 'px-3 py-1.5 text-xs',
  md: 'px-4 py-2 text-sm',
}

export function Button({
  loading,
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
      className={`inline-flex items-center justify-center gap-2 font-medium tracking-wide transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-offset-1 disabled:opacity-50 ${variants[variant]} ${sizes[size]} ${className}`}
      {...props}
    >
      {loading && (
        <svg className="h-4 w-4 animate-spin" viewBox="0 0 24 24" fill="none" aria-hidden>
          <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
          <path
            className="opacity-75"
            fill="currentColor"
            d="M4 12a8 8 0 018-8v4a4 4 0 00-4 4H4z"
          />
        </svg>
      )}
      {children}
    </button>
  )
}
