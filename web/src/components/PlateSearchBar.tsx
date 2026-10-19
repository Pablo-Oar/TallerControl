import { useState } from 'react'
import { useNavigate } from 'react-router'

/** Barra de búsqueda por patente. Navega a /app/vehicles?q={plate}. */
export function PlateSearchBar() {
  const navigate = useNavigate()
  const [value, setValue] = useState('')

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    const plate = value.trim().toUpperCase()
    if (!plate) return
    navigate(`/app/vehicles?q=${encodeURIComponent(plate)}`)
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
        className="w-36 border border-tc-navy-mid bg-tc-navy-surface px-2 py-1 text-sm text-white placeholder:text-tc-navy-text outline-none focus:w-48 focus:border-tc-navy-text transition-all"
      />
      <button
        type="submit"
        className="border border-white/40 bg-white/10 px-2 py-1 text-sm text-white hover:bg-white/20 hover:border-white/70 transition-colors disabled:opacity-30"
        disabled={!value.trim()}
      >
        Ir
      </button>
    </form>
  )
}
