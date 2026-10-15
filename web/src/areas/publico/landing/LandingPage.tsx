import { Link } from 'react-router'

const features = [
  {
    icon: '🚗',
    title: 'Historial del vehículo',
    desc: 'Registrá cada servicio, reparación y cambio de aceite. Todo el historial del vehículo en un solo lugar.',
  },
  {
    icon: '📋',
    title: 'Presupuestos y órdenes',
    desc: 'Generá presupuestos profesionales, convertílos en órdenes de trabajo y facturá en segundos.',
  },
  {
    icon: '💳',
    title: 'Cobros y pagos',
    desc: 'Registrá pagos parciales o totales, emitís comprobantes y controlás la deuda de cada cliente.',
  },
  {
    icon: '🔒',
    title: 'Portal QR seguro',
    desc: 'El dueño del vehículo escanea el QR y ve el estado de su auto. Sin contraseñas ni apps extra.',
  },
  {
    icon: '📦',
    title: 'Catálogo y stock',
    desc: 'Controlá repuestos y servicios. Alertas de stock bajo para que nunca te falte lo que necesitás.',
  },
  {
    icon: '👷',
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
    <div className="min-h-screen bg-white text-slate-900">
      {/* Nav */}
      <nav className="flex items-center justify-between px-6 py-4 border-b border-slate-100">
        <span className="text-xl font-bold text-blue-600">TallerControl</span>
        <div className="flex items-center gap-4 text-sm">
          <Link to="/login" className="text-slate-600 hover:text-slate-900">
            Ingresar
          </Link>
          <Link
            to="/registro"
            className="rounded-lg bg-blue-600 px-4 py-2 font-medium text-white hover:bg-blue-700"
          >
            Crear cuenta
          </Link>
        </div>
      </nav>

      {/* Hero */}
      <section className="mx-auto max-w-4xl px-6 py-20 text-center">
        <h1 className="mb-6 text-4xl font-bold leading-tight text-slate-900 sm:text-5xl">
          El sistema para tu taller mecánico
        </h1>
        <p className="mb-8 text-lg text-slate-600 max-w-2xl mx-auto">
          Gestioná presupuestos, órdenes de trabajo y cobros. Dale a tus clientes un portal QR para
          seguir el estado de su vehículo. Todo desde el celular o la PC.
        </p>
        <div className="flex flex-col sm:flex-row gap-3 justify-center">
          <Link
            to="/registro"
            className="rounded-lg bg-blue-600 px-6 py-3 font-semibold text-white hover:bg-blue-700"
          >
            Empezar gratis
          </Link>
          <Link
            to="/login"
            className="rounded-lg border border-slate-300 px-6 py-3 font-semibold text-slate-700 hover:border-slate-400"
          >
            Ya tengo cuenta
          </Link>
        </div>
      </section>

      {/* Features */}
      <section className="bg-slate-50 px-6 py-16">
        <div className="mx-auto max-w-5xl">
          <h2 className="mb-10 text-center text-2xl font-bold text-slate-900">
            Todo lo que necesitás en un solo lugar
          </h2>
          <div className="grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
            {features.map((f) => (
              <div
                key={f.title}
                className="rounded-xl border border-slate-200 bg-white p-6 shadow-sm"
              >
                <div className="mb-3 text-3xl">{f.icon}</div>
                <h3 className="mb-1 font-semibold text-slate-900">{f.title}</h3>
                <p className="text-sm text-slate-600">{f.desc}</p>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* Pricing */}
      <section className="px-6 py-16">
        <div className="mx-auto max-w-3xl">
          <h2 className="mb-10 text-center text-2xl font-bold text-slate-900">Planes</h2>
          <div className="grid gap-6 sm:grid-cols-2">
            {plans.map((plan) => (
              <div
                key={plan.name}
                className={`rounded-xl border p-6 ${
                  plan.highlight
                    ? 'border-blue-500 bg-blue-50 shadow-md'
                    : 'border-slate-200 bg-white shadow-sm'
                }`}
              >
                {plan.highlight && (
                  <span className="mb-3 inline-block rounded-full bg-blue-600 px-3 py-1 text-xs font-semibold text-white">
                    Próximamente
                  </span>
                )}
                <h3 className="text-lg font-bold text-slate-900">{plan.name}</h3>
                <div className="my-2">
                  <span className="text-3xl font-bold text-slate-900">{plan.price}</span>
                  <span className="ml-1 text-sm text-slate-500">{plan.period}</span>
                </div>
                <p className="mb-4 text-sm text-slate-600">{plan.desc}</p>
                <ul className="mb-6 space-y-2">
                  {plan.features.map((feat) => (
                    <li key={feat} className="flex items-center gap-2 text-sm text-slate-700">
                      <span className="text-green-500">✓</span>
                      {feat}
                    </li>
                  ))}
                </ul>
                <Link
                  to={plan.href}
                  className={`block w-full rounded-lg py-2.5 text-center text-sm font-semibold ${
                    plan.highlight
                      ? 'bg-slate-200 text-slate-500 cursor-not-allowed pointer-events-none'
                      : 'bg-blue-600 text-white hover:bg-blue-700'
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
      <footer className="border-t border-slate-100 px-6 py-8 text-center text-sm text-slate-400">
        <p>© 2026 TallerControl</p>
        <p className="mt-1">
          <Link to="/privacidad" className="hover:text-slate-600">
            Aviso de privacidad
          </Link>
        </p>
      </footer>
    </div>
  )
}
