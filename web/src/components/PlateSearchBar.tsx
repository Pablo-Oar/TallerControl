import { useState } from 'react'

interface PlateSearchBarProps {
  onNavigate: (url: string) => void
  /** Pantalla a la que va la búsqueda (`?q=`). Por defecto los vehículos del taller; el mecánico usa `/m`. */
  target?: string
}

/** Barra de búsqueda por patente. Llama onNavigate con {target}?q={plate}. */
export function PlateSearchBar({ onNavigate, target = '/app/vehicles' }: PlateSearchBarProps) {
  const [value, setValue] = useState('')

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    const plate = value.trim().toUpperCase()
    if (!plate) return
    onNavigate(`${target}?q=${encodeURIComponent(plate)}`)
    setValue('')
  }

  return (
    <form onSubmit={handleSubmit} className="flex items-center gap-1">
      <input
        type="search"
        value={value}
        onChange={e => setValue(e.target.value)}
        placeholder="Buscar patente…"
        aria-label="Buscar patente"
        autoCapitalize="characters"
        autoCorrect="off"
        autoComplete="off"
        spellCheck={false}
        enterKeyHint="search"
        className="w-36 border border-tc-navy-mid bg-tc-navy-surface px-2 py-1 text-sm text-white placeholder:text-tc-navy-text outline-none focus:w-48 focus:border-tc-navy-text transition-all"
      />
      <button
        type="submit"
        className="min-h-[36px] border border-white/40 bg-white/10 px-3 py-1 text-sm font-bold text-white hover:bg-white/20 hover:border-white/70 transition-colors disabled:opacity-50"
        disabled={!value.trim()}
      >
        Ir
      </button>
    </form>
  )
}
