import { Link } from 'react-router'
import { Pricing3, type PricingPlan } from '../../../components/ui/Pricing3'
import { FeatureShowcase } from './FeatureShowcase'
import { SignalPulse } from '../../../components/ui/SignalPulse'

const plans: PricingPlan[] = [
  {
    id: 'trial',
    name: 'Prueba gratis',
    price: '$0',
    period: 'durante 10 días',
    description: 'Probá TallerControl con tu taller real, sin tarjeta y sin compromiso.',
    features: [
      'Acceso completo durante 10 días',
      'Órdenes, presupuestos y cobros',
      'Portal QR para tus clientes',
      'Portal del mecánico en el celular',
      'Soporte por email',
    ],
    buttonText: 'Probar gratis',
    href: '/registro',
  },
  {
    id: 'pro',
    name: 'Inicia ahora',
    price: '$50.000',
    period: 'por mes',
    description: 'Todo lo que necesita tu taller para trabajar sin papeles ni planillas.',
    features: [
      'Sucursales y usuarios ilimitados',
      'Órdenes y presupuestos ilimitados',
      'Facturación electrónica ARCA',
      'Caja, compras y reportes con Excel',
      'Soporte prioritario',
    ],
    buttonText: 'Iniciar ahora',
    href: '/registro',
    isPopular: true,
    popularBadgeText: 'Recomendado',
  },
]

export function LandingPage() {
  return (
    <div className="min-h-screen bg-white text-tc-text">
      {/* Nav */}
      <nav className="flex items-center justify-between border-b border-tc-border bg-tc-navy px-6 py-3">
        <span className="text-[10px] font-bold tracking-[0.2em] uppercase text-white">TallerControl</span>
        <div className="flex items-center gap-2">
          <Link to="/login" className="inline-flex items-center justify-center min-h-[44px] px-4 text-xs font-bold tracking-wider uppercase text-slate-300 hover:text-white transition-colors">
            Ingresar
          </Link>
          <Link
            to="/registro"
            className="inline-flex items-center justify-center min-h-[44px] border border-white px-4 text-xs font-bold tracking-wider uppercase text-white hover:bg-white hover:text-tc-navy transition-colors"
          >
            Crear cuenta
          </Link>
        </div>
      </nav>

      {/* Hero */}
      <section className="relative isolate overflow-hidden bg-tc-navy px-6 py-20 text-center">
        <SignalPulse accentColor="#8b5cf6" className="absolute inset-0 -z-10 text-white" />
        <div className="relative">
        <p className="mb-3 text-[10px] font-bold tracking-[0.2em] uppercase text-tc-text-muted">Sistema de gestión para talleres mecánicos</p>
        <h1 className="mb-6 text-4xl font-bold leading-tight text-white sm:text-5xl">
          El control total de<br className="hidden sm:block" /> tu taller
        </h1>
        <p className="mb-10 mx-auto max-w-2xl text-tc-text-muted">
          Gestioná presupuestos, órdenes de trabajo y cobros. Dale a tus clientes un portal QR para
          seguir el estado de su vehículo. Todo desde el celular o la PC.
        </p>
        <div className="flex flex-col sm:flex-row gap-3 justify-center">
          <Link
            to="/registro"
            className="border border-white bg-white px-6 py-3 text-sm font-bold tracking-wider uppercase text-tc-navy hover:bg-transparent hover:text-white transition-colors"
          >
            Empezar gratis
          </Link>
          <Link
            to="/login"
            className="border border-slate-600 px-6 py-3 text-sm font-bold tracking-wider uppercase text-slate-300 hover:border-white hover:text-white transition-colors"
          >
            Ya tengo cuenta
          </Link>
        </div>
        </div>
      </section>

      <FeatureShowcase />

      {/* Pricing */}
      <Pricing3
        badgeText="Planes"
        title="Empezá gratis, seguí cuando te convenza"
        subtitle="Diez días con todo incluido para probarlo en tu taller. Después, un único plan sin letra chica."
        plans={plans}
        footerText="Precios en pesos argentinos."
      />

      {/* Footer */}
      <footer className="border-t border-tc-border bg-tc-navy px-6 py-8 text-center text-xs text-tc-text-secondary">
        <p className="font-bold tracking-wider uppercase">TallerControl</p>
        <p className="mt-2">© 2026</p>
        <p className="mt-1">
          <Link to="/privacidad" className="hover:text-slate-300 transition-colors">
            Aviso de privacidad
          </Link>
        </p>
      </footer>
    </div>
  )
}
