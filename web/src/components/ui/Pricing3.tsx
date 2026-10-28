import { Link } from 'react-router'
import { ArrowRightIcon } from './icons'

export interface PricingPlan {
  id: string
  name: string
  price: string
  /** Texto chico junto al precio ("/mes", "10 días"). */
  period?: string
  description: string
  features: string[]
  buttonText: string
  href: string
  isPopular?: boolean
  popularBadgeText?: string
}

interface Pricing3Props {
  badgeText?: string
  title: string
  subtitle: string
  plans: PricingPlan[]
  footerText?: string
}

function CheckCircle() {
  return (
    <svg viewBox="0 0 20 20" aria-hidden="true" className="mt-0.5 h-5 w-5 shrink-0 text-tc-purple">
      <circle cx="10" cy="10" r="10" fill="currentColor" />
      <path d="M5.5 10.5l3 3 6-6.5" fill="none" stroke="#0a1628" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  )
}

/** Sección de planes (idea de `pricing-3` de Watermelon; código propio, sin dependencias de íconos). */
export function Pricing3({ badgeText, title, subtitle, plans, footerText }: Pricing3Props) {
  return (
    <section className="bg-tc-navy px-6 py-16 md:py-24">
      <div className="mx-auto max-w-5xl">
        <div className="mb-14 flex flex-col items-start gap-5">
          {badgeText && (
            <span className="inline-flex items-center rounded-full border border-white/15 bg-white/5 px-3 py-1 text-xs font-medium text-white">
              {badgeText}
            </span>
          )}
          <h2 className="max-w-2xl text-4xl font-bold tracking-tight text-white md:text-5xl">{title}</h2>
          <p className="max-w-2xl text-lg leading-relaxed text-tc-text-muted">{subtitle}</p>
        </div>

        <div className="grid grid-cols-1 gap-8 lg:grid-cols-2">
          {plans.map(plan => (
            <div
              key={plan.id}
              className={[
                'group/plan relative flex flex-col gap-8 rounded-[32px] border bg-white/[0.04] p-6 transition-all duration-300 hover:border-tc-purple hover:bg-white/[0.08] hover:shadow-[0_32px_60px_-14px_rgba(124,58,237,0.5)] sm:flex-row sm:p-8 motion-safe:hover:-translate-y-3 motion-safe:hover:scale-[1.02]',
                plan.isPopular ? 'border-tc-purple ring-1 ring-tc-purple/30' : 'border-white/10',
              ].join(' ')}
            >
              {plan.isPopular && plan.popularBadgeText && (
                <span className="absolute -top-3 left-6 rounded-md bg-tc-purple px-4 py-1 text-xs font-semibold text-white shadow-sm sm:left-8">
                  {plan.popularBadgeText}
                </span>
              )}

              <div className="flex flex-1 flex-col items-start border-b border-white/10 pb-8 sm:border-r sm:border-b-0 sm:pr-8 sm:pb-0">
                <span className="mb-4 rounded-full border border-white/15 bg-white/5 px-3 py-0.5 text-xs font-medium text-white">
                  {plan.name}
                </span>
                <div className="mb-4 flex flex-wrap items-baseline gap-x-2">
                  <span className="text-5xl font-extrabold tracking-tight text-white">{plan.price}</span>
                  {plan.period && <span className="whitespace-nowrap text-sm text-tc-text-muted">{plan.period}</span>}
                </div>
                <p className="mb-8 text-sm leading-relaxed text-tc-text-muted">{plan.description}</p>
                <Link
                  to={plan.href}
                  className={[
                    'group mt-auto inline-flex min-h-12 whitespace-nowrap w-full items-center justify-center gap-2 rounded-full px-6 text-sm font-semibold transition-colors sm:w-auto',
                    plan.isPopular
                      ? 'bg-tc-purple text-white hover:bg-tc-purple-mid'
                      : 'bg-white/10 text-white hover:bg-white/20',
                  ].join(' ')}
                >
                  {plan.buttonText}
                  <ArrowRightIcon className="h-4 w-4 transition-transform group-hover:translate-x-1" />
                </Link>
              </div>

              <ul className="flex flex-1 flex-col justify-center gap-4 sm:pl-2">
                {plan.features.map(feature => (
                  <li key={feature} className="flex items-start gap-3">
                    <CheckCircle />
                    <span className="text-sm font-medium text-white">{feature}</span>
                  </li>
                ))}
              </ul>
            </div>
          ))}
        </div>

        {footerText && <p className="mx-auto mt-14 max-w-3xl text-center text-sm leading-relaxed text-tc-text-muted">{footerText}</p>}
      </div>
    </section>
  )
}
