import { useState } from 'react'
import { useSearchParams } from 'react-router'

/**
 * Estado «creando» de una lista que puede abrirse con `?nueva=1` (acciones rápidas del Inicio).
 * Al cerrar el diálogo se quita el parámetro para que recargar la página no lo vuelva a abrir.
 */
export function useCreateFromQuery() {
  const [params, setParams] = useSearchParams()
  const [creating, setCreatingState] = useState(params.get('nueva') === '1')

  function setCreating(value: boolean) {
    setCreatingState(value)
    if (!value && params.has('nueva')) {
      const next = new URLSearchParams(params)
      next.delete('nueva')
      setParams(next, { replace: true })
    }
  }

  return [creating, setCreating] as const
}
