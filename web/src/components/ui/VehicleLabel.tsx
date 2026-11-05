interface VehicleLabelProps {
  plate?: string | null
  brand?: string | null
  model?: string | null
  /** En una sola línea («Toyota Corolla · ABC123»); por defecto, en dos: marca y modelo arriba, patente abajo. */
  inline?: boolean
  className?: string
}

/** Vehículo como lo reconoce el taller: marca y modelo, y la patente en monoespaciado. Se usa en todas las listas. */
export function VehicleLabel({ plate, brand, model, inline = false, className = '' }: VehicleLabelProps) {
  const name = [brand, model].filter(Boolean).join(' ')
  const plateEl = plate ? (
    <span className="font-mono text-[11px] font-bold tracking-widest text-tc-text-muted">{plate}</span>
  ) : null

  if (inline) {
    return (
      <span className={`min-w-0 ${className}`}>
        {name && <span className="text-tc-text">{name}</span>}
        {name && plateEl && <span className="text-tc-text-muted"> · </span>}
        {plateEl ?? (!name && <span className="text-tc-text-muted">—</span>)}
      </span>
    )
  }

  return (
    <div className={`min-w-0 ${className}`}>
      <p className="truncate text-sm text-tc-text">{name || plate || '—'}</p>
      {name && plateEl && <p className="mt-0.5 truncate">{plateEl}</p>}
    </div>
  )
}
