import type { ReactNode } from 'react'

interface ModalProps {
  title: string
  onClose: () => void
  children: ReactNode
  /** Extra classes on the panel (e.g. max-w-lg) */
  className?: string
}

/** Jet-Age Ticket Wallet modal chrome. Carrier-navy header, white body. */
export function Modal({ title, onClose, children, className = 'max-w-lg' }: ModalProps) {
  return (
    <div
      className="fixed inset-0 z-50 flex items-start justify-center overflow-y-auto bg-black/60 p-4 pt-16"
      onClick={e => { if (e.target === e.currentTarget) onClose() }}
    >
      <div className={`relative w-full ${className} bg-white shadow-2xl`} role="dialog" aria-modal>
        {/* Header */}
        <div className="flex items-center justify-between bg-tc-navy px-5 py-3">
          <h2 className="text-sm font-bold tracking-[0.1em] uppercase text-white">{title}</h2>
          <button
            onClick={onClose}
            className="flex h-6 w-6 items-center justify-center text-tc-navy-text hover:text-white transition-colors"
            aria-label="Cerrar"
          >
            <svg className="h-4 w-4" viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden>
              <path d="M3 3l10 10M13 3L3 13" strokeLinecap="round" />
            </svg>
          </button>
        </div>
        {/* Perforation line */}
        <div className="h-px w-full bg-tc-perf" />
        {/* Body */}
        <div className="p-5">
          {children}
        </div>
      </div>
    </div>
  )
}
