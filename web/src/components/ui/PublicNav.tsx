import { Link } from 'react-router'

/** Barra superior de las páginas públicas (landing y pantallas de acceso): la marca lleva a la landing. */
export function PublicNav() {
  return (
    <nav className="sticky top-0 z-40 border-b border-tc-border bg-tc-navy px-4 py-3 sm:px-6">
      <div className="mx-auto flex max-w-6xl items-center justify-between">
        <Link
          to="/"
          aria-label="TallerControl, ir al inicio"
          className="inline-flex min-h-[44px] items-center text-xs font-bold tracking-[0.15em] uppercase text-white sm:text-sm sm:tracking-[0.2em]"
        >
          Taller<span className="text-tc-purple">Control</span>
        </Link>
        <div className="flex items-center gap-1 sm:gap-2">
          <Link
            to="/login"
            className="inline-flex min-h-[44px] items-center justify-center px-3 text-xs font-bold tracking-wider uppercase text-slate-300 transition-colors hover:text-white sm:px-4"
          >
            Ingresar
          </Link>
          <Link
            to="/registro"
            className="inline-flex min-h-[44px] items-center justify-center whitespace-nowrap border border-white px-3 text-xs font-bold tracking-wider uppercase text-white transition-colors hover:bg-white hover:text-tc-navy sm:px-4"
          >
            Crear cuenta
          </Link>
        </div>
      </div>
    </nav>
  )
}
