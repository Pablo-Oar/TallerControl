import { TabNav } from '../../../components/ui/Tabs'

/** Navegación de la Agenda: lista de turnos · calendario semanal · horarios y días especiales. */
export function AppointmentsTabs() {
  return (
    <TabNav
      label="Agenda"
      items={[
        { to: '/app/appointments', label: 'Turnos', end: true },
        { to: '/app/appointments/calendar', label: 'Calendario' },
        { to: '/app/appointments/schedule', label: 'Horarios' },
      ]}
    />
  )
}
