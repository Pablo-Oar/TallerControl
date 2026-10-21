import { Link } from 'react-router'
import { SignalPulse } from '../../../components/ui/SignalPulse'

const features = [
  {
    icon: '⊙',
    title: 'Historial del vehículo',
    desc: 'Registrá cada servicio, reparación y cambio de aceite. Todo el historial del vehículo en un solo lugar.',
  },
  {
    icon: '≡',
    title: 'Presupuestos y órdenes',
    desc: 'Generá presupuestos profesionales, convertílos en órdenes de trabajo y facturá en segundos.',
  },
  {
    icon: '◈',
    title: 'Cobros y pagos',
    desc: 'Registrá pagos parciales o totales, emitís comprobantes y controlás la deuda de cada cliente.',
  },
  {
    icon: '▣',
    title: 'Portal QR seguro',
    desc: 'El dueño del vehículo escanea el QR y ve el estado de su auto. Sin contraseñas ni apps extra.',
  },
  {
    icon: '⊞',
    title: 'Catálogo y stock',
    desc: 'Controlá repuestos y servicios. Alertas de stock bajo para que nunca te falte lo que necesitás.',
  },
  {
    icon: '◧',
    title: 'Portal del mecánico',
    desc: 'Vista optimizada para el celular: las OT asignadas, notas del trabajo y estado actualizado.',
  },
]

const plans = [
  {
    name: 'Gratis',
    price: '$0',
    period: 'para siempre',
    desc: 'Para talleres que están empezando.',
    features: ['1 sucursal', 'Hasta 100 órdenes/mes', 'Portal QR incluido', 'Soporte por email'],
    cta: 'Empezar gratis',
    href: '/registro',
    highlight: false,
  },
  {
    name: 'Pro',
    price: '$9.990',
    period: 'por mes',
    desc: 'Para talleres en crecimiento.',
    features: ['Sucursales ilimitadas', 'Órdenes ilimitadas', 'Reportes avanzados', 'Soporte prioritario'],
    cta: 'Próximamente',
    href: '#',
    highlight: true,
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

      {/* Features */}
      <section className="bg-tc-surface px-6 py-16 border-t border-tc-border">
        <div className="mx-auto max-w-5xl">
          <p className="mb-2 text-center text-[10px] font-bold tracking-[0.2em] uppercase text-tc-text-muted">Funcionalidades</p>
          <h2 className="mb-10 text-center text-2xl font-bold text-tc-text">
            Todo lo que necesitás en un solo lugar
          </h2>
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {features.map((f) => (
              <div
                key={f.title}
                className="border border-tc-border bg-white p-6"
              >
                <div className="mb-3 flex items-center gap-2">
                  <span className="text-tc-navy text-xl font-mono">{f.icon}</span>
                  <h3 className="font-bold text-tc-text">{f.title}</h3>
                </div>
                <p className="text-sm text-tc-text-secondary">{f.desc}</p>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* Pricing */}
      <section className="px-6 py-16 border-t border-tc-border">
        <div className="mx-auto max-w-3xl">
          <p className="mb-2 text-center text-[10px] font-bold tracking-[0.2em] uppercase text-tc-text-muted">Precios</p>
          <h2 className="mb-10 text-center text-2xl font-bold text-tc-text">Planes</h2>
          <div className="grid gap-6 sm:grid-cols-2">
            {plans.map((plan) => (
              <div
                key={plan.name}
                className={`border p-6 ${
                  plan.highlight
                    ? 'border-tc-navy bg-tc-navy text-white'
                    : 'border-tc-border bg-white'
                }`}
              >
                {plan.highlight && (
                  <span className="mb-3 inline-block border border-white px-2 py-0.5 text-[10px] font-bold tracking-wider uppercase text-white">
                    Próximamente
                  </span>
                )}
                <h3 className={`text-lg font-bold ${plan.highlight ? 'text-white' : 'text-tc-text'}`}>{plan.name}</h3>
                <div className="my-2">
                  <span className={`text-3xl font-bold ${plan.highlight ? 'text-white' : 'text-tc-text'}`}>{plan.price}</span>
                  <span className={`ml-1 text-sm ${plan.highlight ? 'text-tc-text-muted' : 'text-tc-text-secondary'}`}>{plan.period}</span>
                </div>
                <p className={`mb-4 text-sm ${plan.highlight ? 'text-slate-300' : 'text-tc-text-secondary'}`}>{plan.desc}</p>
                <ul className="mb-6 space-y-2">
                  {plan.features.map((feat) => (
                    <li key={feat} className={`flex items-center gap-2 text-sm ${plan.highlight ? 'text-slate-200' : 'text-tc-text-secondary'}`}>
                      <span className={plan.highlight ? 'text-tc-text-muted' : 'text-tc-text-muted'}>—</span>
                      {feat}
                    </li>
                  ))}
                </ul>
                <Link
                  to={plan.href}
                  className={`flex items-center justify-center w-full min-h-[44px] text-xs font-bold tracking-wider uppercase transition-colors ${
                    plan.highlight
                      ? 'border border-slate-600 bg-transparent text-tc-text-muted cursor-not-allowed pointer-events-none'
                      : 'border border-tc-navy bg-tc-navy text-white hover:bg-transparent hover:text-tc-navy'
                  }`}
                >
                  {plan.cta}
                </Link>
              </div>
            ))}
          </div>
        </div>
      </section>

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
