import type { FormEvent } from 'react'

/**
 * Los formularios con `required`/`type="email"` nativos mostraban la burbuja del navegador (idioma del
 * navegador, tapa el campo siguiente en el celular). Este manejador la cancela y deja el mensaje, en
 * español, en el cuadro de error del propio formulario.
 */
export function spanishInvalid(setError: (message: string) => void) {
  return (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault()
    const el = e.target as HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement
    const label = el.labels?.[0]?.textContent?.replace(/\*/g, '').trim() || 'Este campo'
    if (el.validity.typeMismatch) setError(`${label}: el formato no es válido.`)
    else setError(`${label} es obligatorio.`)
  }
}
