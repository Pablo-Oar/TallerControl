import { useState } from 'react'
import { customersApi, type CustomerListDto } from '../../lib/api/customers'
import { vehiclesApi, type VehicleListDto } from '../../lib/api/vehicles'
import { catalogApi, type CatalogItemKind, type CatalogItemListDto } from '../../lib/api/catalog'
import { SearchPicker, type SearchPickerProps } from './SearchPicker'

type Common<T> = Pick<
  SearchPickerProps<T>,
  'value' | 'onChange' | 'error' | 'requiredMark' | 'size' | 'className' | 'disabled'
> & { label?: string }

/** Cliente por nombre, teléfono o documento (reemplaza el UUID tipeado). */
export function CustomerPicker({ label = 'Cliente', ...rest }: Common<CustomerListDto>) {
  return (
    <SearchPicker<CustomerListDto>
      label={label}
      searchKey="customers"
      search={async q => (await customersApi.search(q || null, 1, 20)).items}
      getId={c => c.id}
      getLabel={c => c.name}
      getHint={c => c.docNumber ?? undefined}
      fetchLabel={async id => (await customersApi.get(id)).name}
      placeholder="Buscar por nombre o documento…"
      {...rest}
    />
  )
}

/** Vehículo por patente, marca o modelo (reemplaza el UUID tipeado). */
export function VehiclePicker({ label = 'Vehículo', ...rest }: Common<VehicleListDto>) {
  return (
    <SearchPicker<VehicleListDto>
      label={label}
      searchKey="vehicles"
      search={async q => (await vehiclesApi.search(q || null, 1, 20)).items}
      getId={v => v.id}
      getLabel={v => v.plate}
      getHint={v => [v.brand, v.model, v.customerName].filter(Boolean).join(' · ') || undefined}
      fetchLabel={async id => (await vehiclesApi.get(id)).plate}
      placeholder="Buscar por patente, marca o modelo…"
      {...rest}
    />
  )
}

/**
 * Ítem del catálogo con búsqueda (reemplaza el select nativo, que no escala con cientos de ítems).
 * Guarda internamente cuál se eligió; `onPick` recibe el ítem (o null al tocar "Cambiar").
 */
export function CatalogPicker({
  label = 'Del catálogo',
  kind,
  initialId = '',
  onPick,
  ...rest
}: Omit<Common<CatalogItemListDto>, 'value' | 'onChange'> & {
  kind?: CatalogItemKind
  initialId?: string
  onPick: (item: CatalogItemListDto | null) => void
}) {
  const [id, setId] = useState(initialId)
  return (
    <SearchPicker<CatalogItemListDto>
      label={label}
      value={id}
      onChange={(newId, item) => {
        setId(newId)
        onPick(item)
      }}
      searchKey={`catalog-${kind ?? 'all'}`}
      search={async q => (await catalogApi.search(q || null, kind ?? null, true, 1, 20)).items}
      getId={c => c.id}
      getLabel={c => c.name}
      getHint={c => [c.sku, c.kind === 'service' ? 'Servicio' : 'Producto'].filter(Boolean).join(' · ') || undefined}
      fetchLabel={async cid => (await catalogApi.get(cid)).name}
      placeholder="Buscar ítem por nombre o SKU…"
      {...rest}
    />
  )
}
