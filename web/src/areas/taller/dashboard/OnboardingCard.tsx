import { useState } from 'react'
import type { OnboardingDto } from '../../../lib/api/onboarding'
import { ChecklistIcon } from './icons'
import { IconTile } from './Widgets'

/**
 * Guía de inicio (solo Dueño). Mientras falten pasos queda a la vista; con más de la mitad hecho arranca
 * plegada a una línea para no ocupar lugar. Completa, deja de mostrarse.
 */
export function OnboardingCard({ onboarding }: { onboarding: OnboardingDto }) {
  const pct = Math.round((onboarding.completedCount / onboarding.totalCount) * 100)
  const [open, setOpen] = useState(onboarding.completedCount * 2 <= onboarding.totalCount)
  const pending = onboarding.steps.filter(step => !step.completed)

  return (
    <section className="overflow-hidden border border-tc-border bg-white" aria-label="Guía de inicio">
      <button
        type="button"
        aria-expanded={open}
        onClick={() => setOpen(v => !v)}
        className="flex min-h-11 w-full items-center justify-between gap-3 border-b border-tc-border bg-tc-stub-bg px-4 py-3 text-left"
      >
        <span className="flex items-center gap-2.5 text-[11px] font-bold tracking-[0.14em] uppercase text-tc-text-secondary">
          <IconTile accent="purple" small><ChecklistIcon /></IconTile>
          Guía de inicio
        </span>
        <span className="flex items-center gap-2 text-[11px] font-bold tracking-wider tabular-nums text-tc-purple">
          {onboarding.completedCount}/{onboarding.totalCount} · {pct}%
          <svg className={['h-3 w-3 transition-transform', open ? 'rotate-180' : ''].join(' ')} viewBox="0 0 12 12" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden>
            <path d="M2 4l4 4 4-4" strokeLinecap="round" strokeLinejoin="round" />
          </svg>
        </span>
      </button>

      <div className="h-0.5 w-full bg-tc-border">
        <div className="h-0.5 bg-tc-purple transition-all" style={{ width: `${pct}%` }} />
      </div>

      {open && (
        <ul className="divide-y divide-tc-border">
          {pending.map(step => (
            <li key={step.key} className="flex items-center gap-3 px-4 py-2.5">
              <span className="h-4 w-4 flex-none rounded-full border-2 border-tc-border-mid" aria-hidden />
              <span className="text-sm text-tc-text-secondary">{step.label}</span>
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}
