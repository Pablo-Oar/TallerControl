import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { configure } from '@testing-library/dom'
import { afterEach } from 'vitest'

// La cadena de lazy routes (/ → /app → /login) puede tardar más de 1000ms
// en un entorno de pruebas con muchos módulos cargados.
configure({ asyncUtilTimeout: 5000 })

afterEach(() => {
  cleanup()
})