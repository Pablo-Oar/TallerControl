---
name: start-etapa
description: Arranca una etapa del roadmap de TallerControl (E0–E9): lee su sección, verifica dependencias y decisiones, propone la rama y un plan de trabajo por bloques antes de escribir código. Usar al comenzar cualquier etapa o bloque grande.
disable-model-invocation: true
argument-hint: "<etapa, p. ej. E3>"
---

# start-etapa

Etapa pedida: `$ARGUMENTS`

## Pasos

1. **Leer** la sección de la etapa en `../documentacion/analisis-vehix/Plan implementaicon/09_roadmap_de_desarrollo.md` (objetivo, requisitos, dependencias, entidades, endpoints, pantallas, pruebas, entregables, riesgos, "Fin").
2. **Leer** los REQ-xx citados en `01` y las pruebas `T-*` en `08`/`11`; el modelo de las entidades en `04`; los contratos en `05`; permisos en `07`.
3. **Verificar prerrequisitos**:
   - Las etapas de las que depende están cerradas (`git log`, estado del repo).
   - Decisiones D-xx que bloquean la etapa (`10` §1 y §4). Si falta alguna, **parar y preguntar**.
   - Cuentas/servicios externos necesarios (email, R2, Turnstile, Docker).
4. **Proponer rama(s)**: `feature/<etapa>-<tema>` desde `Develop`, una por bloque coherente. Esperar confirmación antes de crearla.
5. **Plan de trabajo**: lista ordenada de bloques (dominio → aplicación → infraestructura → API → UI → pruebas), cada uno con su criterio de terminado, y qué `T-*` cubre.
6. **Definition of done** de la etapa = la línea "Fin" del roadmap + `dotnet test` y `npm --prefix web run test`/`lint` en verde + `security-reviewer` y `architecture-reviewer` sin hallazgos críticos.

No escribir código hasta que el usuario apruebe el plan de trabajo.
