import type { ReactNode } from 'react'

/* ── Ilustraciones: maquetas mínimas de la propia app (decorativas) ───────── */

const panel = 'rounded-xl border border-white/10 bg-white/[0.06] p-3'

function Chip({ children, tone = 'muted' }: { children: ReactNode; tone?: 'muted' | 'purple' | 'green' | 'amber' }) {
  const tones = {
    muted: 'bg-white/10 text-slate-200',
    purple: 'bg-tc-purple/30 text-violet-200',
    green: 'bg-emerald-400/20 text-emerald-200',
    amber: 'bg-amber-400/20 text-amber-200',
  }
  return <span className={`rounded-full px-2 py-0.5 text-[11px] font-semibold ${tones[tone]}`}>{children}</span>
}

function HistoryArt() {
  const rows = [
    ['Cambio de aceite y filtro', '12/03 · 48.200 km'],
    ['Pastillas de freno', '02/07 · 55.900 km'],
    ['Service de 60.000 km', '19/10 · 60.150 km'],
  ]
  return (
    <div className="space-y-2">
      <div className="flex items-center justify-between">
        <span className="font-mono text-xs font-bold tracking-widest text-white">AB 123 CD</span>
        <Chip tone="purple">3 servicios</Chip>
      </div>
      {rows.map(([t, d], i) => (
        <div key={t} className={`${panel} flex items-center gap-3 py-2`}>
          <span className={`h-2 w-2 rounded-full ${i === 2 ? 'bg-tc-purple' : 'bg-white/30'}`} />
          <div className="min-w-0">
            <p className="truncate text-xs font-medium text-white">{t}</p>
            <p className="text-[11px] text-slate-400">{d}</p>
          </div>
        </div>
      ))}
    </div>
  )
}

function QuoteArt() {
  return (
    <div className="space-y-2">
      <div className={panel}>
        <div className="flex items-center justify-between">
          <span className="text-xs font-semibold text-white">Presupuesto #214</span>
          <Chip tone="green">Aprobado</Chip>
        </div>
        <div className="mt-2 space-y-1 text-[11px] text-slate-300">
          <div className="flex justify-between"><span>Cambio de aceite</span><span>$ 8.500</span></div>
          <div className="flex justify-between"><span>Filtro de aire</span><span>$ 2.800</span></div>
        </div>
      </div>
      <div className="flex items-center justify-center text-slate-500" aria-hidden="true">↓ un clic</div>
      <div className={`${panel} flex items-center justify-between`}>
        <span className="text-xs font-semibold text-white">Orden #1042</span>
        <Chip tone="purple">En proceso</Chip>
      </div>
    </div>
  )
}

function PaymentArt() {
  return (
    <div className="space-y-3">
      <div className={panel}>
        <div className="flex items-baseline justify-between">
          <span className="text-[11px] text-slate-400">Cobrado</span>
          <span className="text-sm font-bold text-white">$ 36.300 <span className="text-[11px] font-normal text-slate-400">de $ 50.000</span></span>
        </div>
        <div className="mt-2 h-2 overflow-hidden rounded-full bg-white/10">
          <div className="h-full w-[72%] rounded-full bg-tc-purple" />
        </div>
      </div>
      <div className={`${panel} space-y-1.5`}>
        <div className="flex items-center justify-between text-[11px] text-slate-300"><span>Efectivo</span><Chip tone="green">Cobrado</Chip></div>
        <div className="flex items-center justify-between text-[11px] text-slate-300"><span>Transferencia</span><Chip tone="green">Cobrado</Chip></div>
        <div className="flex items-center justify-between text-[11px] text-slate-300"><span>Saldo</span><Chip tone="amber">$ 13.700</Chip></div>
      </div>
    </div>
  )
}

function QrArt() {
  // Patrón fijo de módulos: solo evoca un QR.
  const cells = '1110111011001011100110101010011101110010101101001110101110010110'.split('')
  return (
    <div className="flex items-center gap-4">
      <div className="grid shrink-0 grid-cols-8 gap-[3px] rounded-lg bg-white p-2">
        {cells.map((c, i) => (
          <span key={i} className={`h-2.5 w-2.5 rounded-[2px] ${c === '1' ? 'bg-tc-navy' : 'bg-transparent'}`} />
        ))}
      </div>
      <div className="min-w-0 space-y-2">
        <p className="text-xs font-semibold text-white">Tu auto, en vivo</p>
        <Chip tone="purple">En proceso</Chip>
        <p className="text-[11px] leading-snug text-slate-400">El dueño escanea, verifica su identidad y ve el estado.</p>
      </div>
    </div>
  )
}

function StockArt() {
  const rows: [string, string, 'muted' | 'amber' | 'green'][] = [
    ['Filtro de aceite', '14 u.', 'green'],
    ['Pastillas delanteras', '2 u.', 'amber'],
    ['Aceite 10W-40 (lt)', '36 lt', 'green'],
  ]
  return (
    <div className="space-y-2">
      {rows.map(([name, qty, tone]) => (
        <div key={name} className={`${panel} flex items-center justify-between py-2`}>
          <span className="text-xs text-white">{name}</span>
          <Chip tone={tone}>{tone === 'amber' ? `Stock bajo · ${qty}` : qty}</Chip>
        </div>
      ))}
    </div>
  )
}

function MechanicArt() {
  return (
    <div className="mx-auto w-40 rounded-[22px] border border-white/20 bg-black/30 p-2">
      <div className="mx-auto mb-2 h-1 w-10 rounded-full bg-white/20" />
      <div className="space-y-1.5">
        <div className={`${panel} p-2`}>
          <p className="text-[11px] font-semibold text-white">OT #1042</p>
          <p className="text-[11px] text-slate-400">AB 123 CD · Service</p>
          <div className="mt-1.5"><Chip tone="purple">En proceso</Chip></div>
        </div>
        <div className="rounded-lg bg-tc-purple px-2 py-1.5 text-center text-[11px] font-bold text-white">Marcar como lista</div>
        <div className="rounded-lg border border-white/15 px-2 py-1.5 text-center text-[11px] text-slate-300">Agregar foto</div>
      </div>
    </div>
  )
}

/* ── Tarjetas ─────────────────────────────────────────────────────────────── */

interface Feature {
  art: ReactNode
  title: string
  desc: string
  points: string[]
}

const features: Feature[] = [
  {
    art: <HistoryArt />,
    title: 'Historial de cada vehículo',
    desc: 'Cada servicio, repuesto y kilometraje queda guardado. Sabés qué se le hizo a cada auto sin buscar en cuadernos ni en el WhatsApp.',
    points: ['Ficha por patente con todo su recorrido', 'Recordatorios automáticos de service', 'Fotos y notas de cada trabajo'],
  },
  {
    art: <QuoteArt />,
    title: 'Presupuestos que se aprueban solos',
    desc: 'Armás el presupuesto en minutos y se lo mandás al cliente por un enlace. Aprueba desde el celular y se convierte en orden con un clic.',
    points: ['Aprobación total o parcial, con firma', 'PDF profesional con el logo de tu taller', 'Sin volver a cargar nada'],
  },
  {
    art: <PaymentArt />,
    title: 'Cobros y caja sin sorpresas',
    desc: 'Registrás pagos parciales o totales, ves cuánto te debe cada cliente y cerrás la caja del día con los números a la vista.',
    points: ['Saldo por cliente y por orden', 'Comprobantes de cobro en PDF', 'Facturación electrónica ARCA integrada'],
  },
  {
    art: <QrArt />,
    title: 'Portal QR para tus clientes',
    desc: 'Cada cliente sigue el estado de su auto sin llamarte. Menos consultas por teléfono y una atención que se nota profesional.',
    points: ['Acceso seguro: la patente sola no alcanza', 'Historial visible para el dueño', 'Aviso si encuentran las llaves de un cliente'],
  },
  {
    art: <StockArt />,
    title: 'Stock y compras bajo control',
    desc: 'Cada repuesto que usás se descuenta solo de la orden. Te avisa cuando algo se está terminando, antes de que frene un trabajo.',
    points: ['Alertas de stock bajo', 'Compras a proveedores con IVA', 'Ventas de mostrador en segundos'],
  },
  {
    art: <MechanicArt />,
    title: 'El taller en el bolsillo del mecánico',
    desc: 'Cada mecánico ve sus órdenes en el celular, actualiza el estado, anota lo que hizo y sube fotos. Sin precios a la vista.',
    points: ['Se instala como app (PWA)', 'Se asigna las órdenes que va tomando', 'Cada rol ve solo lo que le corresponde'],
  },
]

function FeatureCard({ f }: { f: Feature }) {
  return (
    <article className="flex flex-col bg-tc-navy text-white">
      <div className="relative overflow-hidden border-b border-white/10 bg-gradient-to-br from-tc-purple/25 via-tc-navy to-tc-navy px-6 py-8">
        <div
          aria-hidden="true"
          className="pointer-events-none absolute -top-12 -right-12 h-40 w-40 rounded-full bg-tc-purple/30 blur-3xl"
        />
        <div aria-hidden="true" className="relative flex h-[210px] flex-col justify-center">
          {f.art}
        </div>
      </div>
      <div className="flex flex-1 flex-col gap-3 p-6 lg:p-8">
        <h3 className="text-xl font-bold leading-snug">{f.title}</h3>
        <p className="text-[15px] leading-6 text-slate-300">{f.desc}</p>
        <ul className="mt-1 space-y-1.5">
          {f.points.map(p => (
            <li key={p} className="flex items-start gap-2 text-sm text-slate-200">
              <span aria-hidden="true" className="mt-[7px] h-1.5 w-1.5 shrink-0 rounded-full bg-tc-purple" />
              {p}
            </li>
          ))}
        </ul>
      </div>
    </article>
  )
}

/* ── Beneficios y equipo ──────────────────────────────────────────────────── */

const benefits = [
  {
    title: 'Orden',
    desc: 'Se terminan los papeles sueltos, los cuadernos y las planillas. Cada auto, cliente y trabajo tiene su lugar y se encuentra en segundos.',
  },
  {
    title: 'Procesos claros',
    desc: 'Del presupuesto al cobro, todos siguen el mismo camino: nadie se olvida de cargar un repuesto ni de cobrar un trabajo entregado.',
  },
  {
    title: 'Automatización',
    desc: 'El sistema descuenta el stock, calcula los totales, avisa los services próximos y arma los comprobantes. Vos dedicás el tiempo al taller.',
  },
  {
    title: 'Control del negocio',
    desc: 'Cuánto cobraste, cuánto te deben, qué se vende y qué falta, con reportes que podés bajar a Excel cuando los necesites.',
  },
]

const team = [
  ['Dueño', 'Ve todo: cobros, deudas, reportes y configuración.'],
  ['Administración', 'Presupuestos, órdenes, caja, compras y facturación.'],
  ['Mecánicos', 'Sus órdenes en el celular, sin acceso a precios.'],
  ['Contador', 'Libro IVA y exportaciones sin pedirte nada.'],
  ['Tus clientes', 'Aprueban presupuestos y siguen su auto desde el QR.'],
] as const

export function FeatureShowcase() {
  return (
    <section className="bg-tc-surface px-6 pt-16 pb-0 md:py-24 border-t border-tc-border">
      <div className="mx-auto max-w-7xl">
        <div className="mx-auto mb-12 max-w-3xl text-center">
          <p className="mb-2 text-xs font-bold tracking-[0.2em] uppercase text-tc-text-secondary">Funcionalidades</p>
          <h2 className="text-3xl font-bold tracking-tight text-tc-text md:text-4xl">
            Todo tu taller en un solo sistema, sin complicarte
          </h2>
          <p className="mt-4 text-lg leading-relaxed text-tc-text-secondary">
            TallerControl reemplaza el cuaderno, la planilla y los mensajes sueltos: ordena el trabajo,
            automatiza lo repetitivo y te muestra cómo viene tu negocio.
          </p>
        </div>

        {/* Tarjetas unidas, una sola pieza redondeada (estilo card-15) */}
        <div className="-mx-6 grid grid-cols-1 gap-px overflow-hidden border-y border-slate-700 sm:mx-0 sm:rounded-2xl sm:border bg-slate-700 shadow-xl shadow-tc-navy/20 md:grid-cols-2 lg:grid-cols-3">
          {features.map(f => (
            <FeatureCard key={f.title} f={f} />
          ))}
        </div>

        {/* Qué cambia */}
        <div className="mt-20">
          <h3 className="mb-8 text-center text-2xl font-bold text-tc-text">Qué cambia cuando ordenás el taller</h3>
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            {benefits.map((b, i) => (
              <div key={b.title} className="rounded-2xl border border-tc-border bg-white p-6">
                <span className="mb-3 inline-flex h-8 w-8 items-center justify-center rounded-full bg-tc-navy text-xs font-bold text-white">
                  {i + 1}
                </span>
                <h4 className="mb-2 text-base font-bold text-tc-text">{b.title}</h4>
                <p className="text-sm leading-6 text-tc-text-secondary">{b.desc}</p>
              </div>
            ))}
          </div>
        </div>

        {/* Para cada persona */}
        <div className="-mx-6 mt-16 bg-tc-navy px-6 py-10 sm:mx-0 sm:rounded-2xl sm:p-8 md:p-10">
          <div className="mb-10 max-w-2xl">
            <h3 className="text-2xl font-bold text-white">Cada persona ve lo que necesita</h3>
            <p className="mt-2 text-white/80">
              Sumás a tu equipo con roles y permisos: cada uno trabaja en lo suyo y nadie ve lo que no debe.
            </p>
          </div>
          <dl className="grid gap-x-10 gap-y-6 sm:grid-cols-2 lg:grid-cols-5">
            {team.map(([who, what]) => (
              <div key={who} className="border-t border-white/15 pt-4">
                <dt className="text-sm font-bold text-white">{who}</dt>
                <dd className="mt-1 text-sm leading-6 text-slate-300">{what}</dd>
              </div>
            ))}
          </dl>
        </div>
        <div aria-hidden="true" className="-mx-6 h-1 bg-tc-purple sm:hidden" />
      </div>
    </section>
  )
}
