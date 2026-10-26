import { useParams } from 'react-router'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { publicAppointmentApi } from '../../../lib/api/appointments'
import { ApiError } from '../../../lib/api/client'
import { Button } from '../../../components/ui/Button'

/** Enlace que recibe el cliente: ve su turno y lo confirma, o avisa «No puedo ir». Sin datos del taller. */
export function PublicAppointmentPage() {
  const { token } = useParams<{ token: string }>()
  const qc = useQueryClient()

  const { data, isLoading, error } = useQuery({
    queryKey: ['public-appointment', token],
    queryFn: () => publicAppointmentApi.get(token!),
    enabled: !!token,
    retry: false,
  })

  const confirm = useMutation({
    mutationFn: () => publicAppointmentApi.confirm(token!),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['public-appointment', token] }),
  })
  const decline = useMutation({
    mutationFn: () => publicAppointmentApi.decline(token!),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['public-appointment', token] }),
  })

  if (isLoading) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-tc-navy">
        <div className="w-48 space-y-2" role="status" aria-label="Cargando">
          <div className="h-2 animate-pulse bg-tc-navy-mid" />
          <div className="h-2 w-3/4 animate-pulse bg-tc-navy-mid" />
        </div>
      </div>
    )
  }

  // Cancelado, completado, vencido o inexistente: el servidor responde lo mismo (404).
  if (declined(decline.isSuccess) || error || !data) {
    return (
      <div className="flex min-h-screen flex-col items-center justify-center bg-tc-navy px-4 text-center">
        <div className="w-full max-w-sm border border-tc-border bg-white p-8">
          <p className="mb-2 text-xs font-bold tracking-[0.14em] text-tc-text-secondary uppercase">
            {decline.isSuccess ? 'Turno cancelado' : 'Turno'}
          </p>
          <p className="font-bold text-tc-text">
            {decline.isSuccess ? 'Avisamos al taller que no vas a poder asistir.' : 'Este enlace ya no está disponible'}
          </p>
          {!decline.isSuccess && (
            <p className="mt-2 text-sm text-tc-text-secondary">
              El turno pudo haberse cancelado o realizado ya. Si lo necesitás, comunicate con el taller.
            </p>
          )}
        </div>
      </div>
    )
  }

  const when = new Date(data.scheduledAt).toLocaleString('es-AR', {
    timeZone: data.timeZone,
    weekday: 'long',
    day: 'numeric',
    month: 'long',
    hour: '2-digit',
    minute: '2-digit',
  })
  const confirmed = data.status === 'confirmed' || confirm.isSuccess
  const failure = confirm.error ?? decline.error

  return (
    <div className="min-h-screen bg-tc-surface">
      <div className="mx-auto max-w-md px-4 py-10">
        <div className="overflow-hidden border border-tc-border bg-white">
          <div className="flex items-center justify-between border-b border-tc-border bg-tc-stub-bg px-6 py-3">
            <span className="text-xs font-bold tracking-[0.14em] text-tc-text-secondary uppercase">Tu turno</span>
            {confirmed && (
              <span className="border border-green-300 px-1.5 py-0.5 text-[11px] font-bold tracking-wider text-green-700 uppercase">
                Confirmado
              </span>
            )}
          </div>

          <div className="space-y-4 px-6 py-5">
            <p className="text-lg font-bold capitalize text-tc-text">{when} hs</p>

            <dl className="space-y-3 text-sm">
              {data.vehicleLabel && (
                <div className="flex justify-between border-b border-tc-border pb-2">
                  <dt className="text-xs font-bold tracking-wider text-tc-text-secondary uppercase">Vehículo</dt>
                  <dd className="text-tc-text-secondary">{data.vehicleLabel}</dd>
                </div>
              )}
              <div className="border-b border-tc-border pb-2">
                <dt className="mb-1 text-xs font-bold tracking-wider text-tc-text-secondary uppercase">Servicios</dt>
                <dd>
                  <ul className="list-inside list-disc text-tc-text-secondary">
                    {data.services.map(s => <li key={s}>{s}</li>)}
                  </ul>
                </dd>
              </div>
              <div className="flex justify-between">
                <dt className="text-xs font-bold tracking-wider text-tc-text-secondary uppercase">Duración estimada</dt>
                <dd className="tabular-nums text-tc-text-secondary">{data.totalDurationMinutes} min</dd>
              </div>
            </dl>

            {failure && (
              <p role="alert" className="border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
                {failure instanceof ApiError && failure.status === 409 ? 'El turno ya estaba confirmado.' : 'No se pudo registrar tu respuesta. Probá de nuevo.'}
              </p>
            )}

            {confirmed ? (
              <p className="text-sm text-tc-text-secondary">¡Listo! Te esperamos. Si no podés venir, avisanos.</p>
            ) : (
              <Button className="w-full" loading={confirm.isPending} onClick={() => confirm.mutate()}>
                Confirmar mi turno
              </Button>
            )}

            <Button
              variant="secondary"
              className="w-full"
              loading={decline.isPending}
              onClick={() => decline.mutate()}
            >
              No puedo ir
            </Button>
          </div>
        </div>
      </div>
    </div>
  )
}

// Un envío exitoso de «No puedo ir» reemplaza la pantalla (el enlace ya no sirve).
const declined = (ok: boolean) => ok
