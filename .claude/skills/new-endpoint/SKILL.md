---
name: new-endpoint
description: Checklist y plantilla para agregar un endpoint REST a la API de TallerControl respetando multiempresa, permisos, DTO, ProblemDetails, auditoría y pruebas T-ISO/T-PERM. Usar al crear o modificar cualquier endpoint.
---

# new-endpoint

Antes de escribir, ubicá el endpoint en `05_contratos_api.md` y su permiso en `07` §3. Si no está definido, preguntá.

## Checklist

1. **Ruta y versión**: `/api/v1/...` (admin) o `/api/public/v1/...` (público). IDs en la URL son UUIDv7.
2. **Autorización**: política declarada en el endpoint (`[Authorize(Policy = "ordenes.editar")]` o equivalente). Reglas dependientes del recurso (asignación, transición) **dentro del caso de uso**.
3. **DTO de entrada**: sin `tenantId`; validación de **formato** únicamente. IDs externos (cliente, vehículo, ítem) verificados contra el taller en el caso de uso → 404 si no pertenecen.
4. **Caso de uso**: clase simple en `Application/<Modulo>/`; el endpoint solo mapea request → handler → response. Sin reglas de negocio en el endpoint.
5. **Transacción**: una sola si toca varios módulos; emails por outbox.
6. **Respuesta**: DTO dedicado; **omitir** (no vaciar) montos/costos según permiso; en público, sin UUID internos ni contacto.
7. **Errores**: `ProblemDetails` RFC 7807 con `traceId`. 404 uniforme para ajeno/inexistente; 409 para transición/estado inválido o conflicto de concurrencia; 422 para validación; 403 solo cuando el recurso es visible y la acción no.
8. **Concurrencia**: si edita una entidad con `xmin`, manejar el conflicto → 409.
9. **Auditoría**: registrar en `audit_events` los cambios relevantes (antes/después, sin datos sensibles).
10. **Rate limit**: obligatorio en rutas públicas y de autenticación (`07` §6).
11. **OpenAPI**: anotado, para regenerar los tipos del frontend (`openapi-typescript`).

## Pruebas mínimas (agente `test-engineer`)

- Camino feliz + validaciones (422).
- **T-ISO**: usuario del taller B → recurso del A = 404.
- **T-PERM**: cada rol contra la matriz (permitido y prohibido).
- Reglas de negocio del caso de uso con sus `T-*` del plan.

Al terminar, correr el agente `security-reviewer` sobre el diff.
