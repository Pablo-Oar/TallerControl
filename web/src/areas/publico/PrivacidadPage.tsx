import { Link } from 'react-router'

export function PrivacidadPage() {
  return (
    <div className="min-h-screen bg-tc-surface py-10 px-4">
      <div className="mx-auto max-w-2xl">
        <div className="mb-6 flex items-center justify-between">
          <span className="text-sm font-bold tracking-[0.2em] uppercase text-tc-text">
            Taller<span className="text-tc-purple">Control</span>
          </span>
          <Link to="/" className="inline-flex min-h-[44px] items-center text-sm font-semibold text-tc-purple hover:underline">
            ← Volver al inicio
          </Link>
        </div>

        <div className="border border-tc-border bg-white p-6 sm:p-8">
          <h1 className="text-2xl font-bold text-tc-text mb-1">
            Aviso de Privacidad
          </h1>
          <p className="text-sm text-tc-text-secondary mb-6">
            Última actualización: octubre de 2026
          </p>

          <section className="mb-6">
            <h2 className="text-base font-semibold text-tc-text mb-2">
              Responsable del tratamiento
            </h2>
            <p className="text-sm text-tc-text-secondary leading-relaxed">
              TallerControl es una plataforma de gestión para talleres mecánicos
              ("el Servicio"). Los datos personales ingresados en el Servicio son
              tratados por el taller que utiliza la plataforma ("el Taller"), quien
              actúa como responsable del tratamiento según la Ley N.° 25.326 de
              Protección de los Datos Personales de la República Argentina.
            </p>
          </section>

          <section className="mb-6">
            <h2 className="text-base font-semibold text-tc-text mb-2">
              Datos que se recopilan
            </h2>
            <ul className="text-sm text-tc-text-secondary leading-relaxed list-disc list-inside space-y-1">
              <li>Nombre y apellido, correo electrónico y teléfono del cliente.</li>
              <li>Datos del vehículo: marca, modelo, año, patente.</li>
              <li>Historial de trabajos realizados, presupuestos y pagos.</li>
              <li>Fotografías adjuntas a órdenes de trabajo (si las hubiera).</li>
            </ul>
          </section>

          <section className="mb-6">
            <h2 className="text-base font-semibold text-tc-text mb-2">
              Finalidad del tratamiento
            </h2>
            <p className="text-sm text-tc-text-secondary leading-relaxed">
              Los datos se utilizan exclusivamente para la gestión de órdenes de
              trabajo, presupuestos y cobros del Taller, así como para el acceso
              seguro al historial del vehículo mediante el sistema de tarjeta QR.
              No se utilizan con fines publicitarios ni se ceden a terceros sin
              consentimiento expreso, salvo obligación legal.
            </p>
          </section>

          <section className="mb-6">
            <h2 className="text-base font-semibold text-tc-text mb-2">
              Almacenamiento y seguridad
            </h2>
            <p className="text-sm text-tc-text-secondary leading-relaxed">
              Los datos se almacenan en servidores con cifrado en tránsito (HTTPS)
              y en reposo. El acceso al historial del vehículo mediante QR requiere
              verificación de identidad (código de cliente o código de un solo uso
              enviado por correo electrónico).
            </p>
          </section>

          <section className="mb-6">
            <h2 className="text-base font-semibold text-tc-text mb-2">
              Derechos del titular
            </h2>
            <p className="text-sm text-tc-text-secondary leading-relaxed">
              De acuerdo con la Ley N.° 25.326, el titular de los datos tiene
              derecho a acceder, rectificar, actualizar y, cuando corresponda,
              suprimir su información personal. Para ejercer estos derechos, debe
              contactar directamente al Taller responsable.
            </p>
            <p className="text-xs text-tc-text-secondary mt-2">
              "El titular de los datos personales tiene la facultad de ejercer el
              derecho de acceso a los mismos en forma gratuita a intervalos no
              inferiores a seis meses, salvo que se acredite un interés legítimo al
              efecto conforme lo establecido en el artículo 14, inciso 3 de la Ley
              N.° 25.326." — DNPDP, Argentina.
            </p>
          </section>

          <section>
            <h2 className="text-base font-semibold text-tc-text mb-2">
              Contacto
            </h2>
            <p className="text-sm text-tc-text-secondary leading-relaxed">
              Para consultas sobre este aviso o sobre el tratamiento de sus datos
              personales, comuníquese con el Taller directamente o escriba a{' '}
              <a
                href="mailto:privacidad@tallercontrol.com.ar"
                className="text-tc-purple hover:underline"
              >
                privacidad@tallercontrol.com.ar
              </a>
              .
            </p>
          </section>
        </div>
      </div>
    </div>
  )
}
