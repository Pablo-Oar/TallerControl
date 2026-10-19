import { customersApi, type CustomerListDto } from '../../lib/api/customers'
import { vehiclesApi, type VehicleListDto } from '../../lib/api/vehicles'
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
