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
        className="w-36 rounded-md border border-slate-300 bg-slate-50 px-2 py-1 text-sm outline-none focus:w-48 focus:ring-2 focus:ring-blue-500 transition-all"
      />
      <button
        type="submit"
        className="rounded-md bg-slate-100 px-2 py-1 text-sm text-slate-600 hover:bg-slate-200 disabled:opacity-40"
        disabled={!value.trim()}
      >
        Ir
      </button>
    </form>
  )
}
