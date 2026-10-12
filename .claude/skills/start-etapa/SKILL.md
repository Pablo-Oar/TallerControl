---
name: start-etapa
description: Arranca una etapa del roadmap de TallerControl (E0–E9): lee su sección, verifica dependencias y decisiones, propone la rama y un plan de trabajo por bloques antes de escribir código. Usar al comenzar cualquier etapa o bloque grande.
disable-model-invocation: true
argument-hint: "<etapa, p. ej. E3>"
---

# start-etapa

Etapa pedida: `$ARGUMENTS`

## Pasos

Fuente: `../documentacion/analisis-vehix/Plan implementaicon/PlanImplementacionV2.html` (secciones `doc-NN`; los cambios V2 están en el bloque `v2-delta` de cada una). Los `.md` de la carpeta son solo atajo; si difieren, manda el HTML.

1. **Leer** la etapa en `doc-09` (objetivo, requisitos, dependencias, entidades, endpoints, pantallas, pruebas, entregables, riesgos, "Fin") y su `v2-delta`.
2. **Leer** los REQ-xx citados en `doc-01` y las pruebas `T-*` en `doc-08`/`doc-11`; el modelo de las entidades en `doc-04`; los contratos en `doc-05`; permisos en `doc-07`; y las reglas del módulo en `doc-13`. Incluir el `v2-delta` de cada una.
3. **Verificar prerrequisitos**:
   - Las etapas de las que depende están cerradas (`git log`, estado del repo).
   - Decisiones D-xx que bloquean la etapa (`10` §1 y §4). Si falta alguna, **parar y preguntar**.
   - Cuentas/servicios externos necesarios (email, R2, Turnstile, Docker).
4. **Proponer rama(s)**: `feature/<etapa>-<tema>` desde `Develop`, una por bloque coherente. Esperar confirmación antes de crearla.
5. **Plan de trabajo**: lista ordenada de bloques (dominio → aplicación → infraestructura → API → UI → pruebas), cada uno con su criterio de terminado, y qué `T-*` cubre.
6. **Definition of done** de la etapa = la línea "Fin" del roadmap + `dotnet test` y `npm --prefix web run test`/`lint` en verde + `security-reviewer` y `architecture-reviewer` sin hallazgos críticos.

No escribir código hasta que el usuario apruebe el plan de trabajo.
