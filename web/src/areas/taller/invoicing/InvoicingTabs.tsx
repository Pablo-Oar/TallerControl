import { TabNav } from '../../../components/ui/Tabs'

/** Navegación de Facturación: Facturas · Libro IVA · Configuración ARCA (antes eran rutas sueltas con botones). */
export function InvoicingTabs() {
  return (
    <TabNav
      label="Facturación"
      items={[
        { to: '/app/invoicing', label: 'Facturas', end: true },
        { to: '/app/invoicing/iva-book', label: 'Libro IVA' },
        { to: '/app/invoicing/setup', label: 'Configuración ARCA' },
      ]}
    />
  )
}
