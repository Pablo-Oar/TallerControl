import { describe, expect, it } from 'vitest'
import { hasPermissions } from './permissions'
import { firstAllowedPath } from '../../areas/taller/TallerLayout'

// T-UI-09: el menú y las acciones se rigen por los permisos de /me, no por el nombre del rol.
describe('permisos en la interfaz', () => {
  it('hasPermissions exige todos los permisos pedidos', () => {
    const user = { permissions: ['customers.read', 'customers.write'] }
    expect(hasPermissions(user, 'customers.read')).toBe(true)
    expect(hasPermissions(user, 'customers.read', 'customers.delete')).toBe(false)
    expect(hasPermissions(null, 'customers.read')).toBe(false)
    expect(hasPermissions({}, 'customers.read')).toBe(false)
  })

  it('un Contador (solo invoicing.read) arranca en Facturación, no en Inicio', () => {
    expect(firstAllowedPath({ permissions: ['invoicing.read'] })).toBe('/app/invoicing')
  })

  it('quien ve el resumen empieza en Inicio', () => {
    expect(firstAllowedPath({ permissions: ['workorders.read', 'payments.read', 'customers.read'] })).toBe('/app')
  })
})
