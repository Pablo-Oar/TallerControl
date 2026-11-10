import { useState } from 'react'
import { cn } from '@/lib/utils'

interface WorkshopBrandProps {
  name?: string | null
  logoUrl?: string | null
  /** `light`: sobre fondo claro; `dark`: sobre el azul marino de los encabezados. */
  tone?: 'light' | 'dark'
  className?: string
}

/**
 * Logo y nombre del taller en las pantallas públicas que ve el cliente (presupuesto, orden, comprobante, turno,
 * portal del dueño). Si el taller no cargó logo, o la imagen no carga, muestra solo el nombre (o nada).
 */
export function WorkshopBrand({ name, logoUrl, tone = 'light', className }: WorkshopBrandProps) {
  const [failed, setFailed] = useState(false)
  const showLogo = !!logoUrl && !failed
  if (!showLogo && !name) return null

  return (
    <div className={cn('flex items-center gap-3', className)}>
      {showLogo && (
        <img
          src={logoUrl}
          alt={name ? `Logo de ${name}` : 'Logo del taller'}
          onError={() => setFailed(true)}
          className="h-12 max-w-[9rem] shrink-0 rounded-lg bg-white object-contain p-1"
        />
      )}
      {name && (
        <span className={cn('min-w-0 truncate text-base font-bold tracking-tight', tone === 'dark' ? 'text-white' : 'text-tc-text')}>
          {name}
        </span>
      )}
    </div>
  )
}
