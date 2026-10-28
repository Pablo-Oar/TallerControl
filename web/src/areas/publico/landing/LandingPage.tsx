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
    name: 'Empezá ahora',
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
    buttonText: 'Empezá ahora',
    href: '/registro',
    isPopular: true,
    popularBadgeText: 'Recomendado',
  },
]

export function LandingPage() {
  return (
    <div className="min-h-screen bg-white text-tc-text">
      {/* Nav */}
      <nav className="border-b border-tc-border bg-tc-navy px-4 py-3 sm:px-6">
       <div className="mx-auto flex max-w-6xl items-center justify-between">
        <span className="text-xs font-bold tracking-[0.15em] uppercase text-white sm:text-sm sm:tracking-[0.2em]">
          Taller<span className="text-tc-purple">Control</span>
        </span>
        <div className="flex items-center gap-1 sm:gap-2">
          <Link to="/login" className="inline-flex items-center justify-center min-h-[44px] px-3 sm:px-4 text-xs font-bold tracking-wider uppercase text-slate-300 hover:text-white transition-colors">
            Ingresar
          </Link>
          <Link
            to="/registro"
            className="inline-flex items-center justify-center whitespace-nowrap min-h-[44px] border border-white px-3 text-xs sm:px-4 font-bold tracking-wider uppercase text-white hover:bg-white hover:text-tc-navy transition-colors"
          >
            Crear cuenta
          </Link>
        </div>
       </div>
      </nav>

      {/* Hero */}
      <section className="relative isolate overflow-hidden bg-tc-navy px-6 py-20 text-center lg:py-24 lg:text-left">
        <SignalPulse accentColor="#8b5cf6" className="absolute inset-0 -z-10 text-white" />
        <div className="relative mx-auto grid max-w-6xl items-center gap-12 lg:grid-cols-2">
          <div>
            <p className="mb-3 text-xs font-bold tracking-[0.2em] uppercase text-slate-300">Sistema de gestión para talleres mecánicos</p>
            <h1 className="mb-6 text-4xl font-bold leading-tight text-white sm:text-5xl">
              El control total de<br className="hidden sm:block" /> tu taller
            </h1>
            <p className="mx-auto mb-10 max-w-2xl text-base text-white/80 lg:mx-0">
              Gestioná presupuestos, órdenes de trabajo y cobros. Dale a tus clientes un portal QR para
              seguir el estado de su vehículo. Todo desde el celular o la PC.
            </p>
            <div className="flex flex-col justify-center gap-3 sm:flex-row lg:justify-start">
              <Link
                to="/registro"
                className="border border-white bg-white px-6 py-3 text-sm font-bold tracking-wider uppercase text-tc-navy hover:bg-transparent hover:text-white transition-colors"
              >
                Empezar gratis
              </Link>
              <Link
                to="/login"
                className="border border-slate-400 px-6 py-3 text-sm font-bold tracking-wider uppercase text-slate-200 hover:border-white hover:text-white transition-colors"
              >
                Ya tengo cuenta
              </Link>
            </div>
          </div>

          <div className="hidden border border-white/20 bg-white/5 p-6 text-left lg:block">
            <p className="mb-4 text-xs font-bold tracking-[0.2em] uppercase text-slate-300">Qué incluye</p>
            <ul className="space-y-4">
              {[
                ['Órdenes de trabajo', 'Con estados, fotos y historial por vehículo.'],
                ['Presupuestos', 'El cliente los aprueba desde un enlace, sin instalar nada.'],
                ['Portal del mecánico', 'Un panel simple para el celular, sin precios a la vista.'],
                ['Tarjeta QR', 'El dueño consulta el historial de su vehículo con verificación.'],
                ['Cobros y facturación', 'Caja, compras y facturación electrónica ARCA.'],
              ].map(([title, text]) => (
                <li key={title} className="border-l-2 border-tc-purple pl-4">
                  <p className="text-sm font-bold text-white">{title}</p>
                  <p className="text-sm text-white/80">{text}</p>
                </li>
              ))}
            </ul>
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
      <footer className="border-t border-tc-border bg-tc-navy px-6 py-8 text-center text-xs text-slate-300">
        <p className="font-bold tracking-wider uppercase">TallerControl</p>
        <p className="mt-2">© 2026</p>
        <p className="mt-1">
          <Link to="/privacidad" className="hover:text-white transition-colors">
            Aviso de privacidad
          </Link>
        </p>
      </footer>
    </div>
  )
}
