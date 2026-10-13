---
name: start-etapa
description: Arranca una etapa del roadmap de TallerControl (E0–E13): analiza el codebase existente con un subagente, lee el plan, verifica dependencias y decisiones, y propone la rama y un plan de trabajo por bloques antes de escribir código. Usar al comenzar cualquier etapa o bloque grande.
disable-model-invocation: true
argument-hint: "<etapa, p. ej. E5>"
---

# start-etapa

Etapa pedida: `$ARGUMENTS`

## Pasos

### Paso 0 — Snapshot del codebase actual (subagente Explore)

**Antes de leer el plan**, lanzar el agente `Explore` con `run_in_background: false` para obtener el estado real del código. Esto evita confiar en CLAUDE.md que puede estar desactualizado y permite detectar colisiones, patrones ya establecidos y módulos ya parcialmente implementados.

Consigna exacta para el subagente:

> "Hace un análisis del codebase de TallerControl para planificar la siguiente etapa de desarrollo. Necesito los datos exactos — no sugerencias ni análisis de calidad.
>
> 1. **Módulos Domain/Application/Infrastructure**: para cada subcarpeta en esos tres proyectos, lista sus archivos `.cs`. Busca en `src/TallerControl.Domain/`, `src/TallerControl.Application/`, `src/TallerControl.Infrastructure/`.
> 2. **Endpoints existentes**: lee cada archivo `*Endpoints.cs` en `src/TallerControl.Api/Endpoints/` y extrae las rutas (strings que empiezan con `/api/`).
> 3. **Permisos existentes**: encuentra la enumeración `Permission` (busca `enum Permission` con Grep) y lista todos sus valores.
> 4. **Entidades con sus relaciones clave**: para cada entidad en Domain, busca sus propiedades de navegación (`public … {get; private set;}` o `public ICollection`) y FK (`TenantId`, `BranchId`, `CustomerId`, etc.). Solo para entidades de los módulos que la etapa $ARGUMENTS dice que depende de.
> 5. **Migraciones**: lista los archivos de migración en `src/TallerControl.Infrastructure/Persistence/Migrations/` (sin Designer.cs ni Snapshot), en orden cronológico, con las tablas que crean/modifican (busca `migrationBuilder.CreateTable` en cada uno).
> 6. **Factories de test**: lista los archivos `*WebApplicationFactory.cs` y `*Fixture.cs` en `tests/` con sus propiedades públicas de tipo `Guid` (son los IDs sembrados para T-ISO-ALL).
> 7. **Pantallas y rutas React**: lista rutas registradas en `TallerArea.tsx` y links en `TallerLayout.tsx`.
> 8. **Interfaces de Application**: lista las interfaces (archivos `I*.cs`) en `src/TallerControl.Application/` y sus métodos.
>
> Devuelve todo como un informe estructurado con esas 8 secciones."

Esperar el resultado del subagente. Usarlo como contexto para todos los pasos siguientes.

---

### Paso 1 — Leer el plan

Fuente: `../documentacion/analisis-vehix/Plan implementaicon/PlanImplementacionV2.html` (secciones `doc-NN`; los cambios V2 están en el bloque `v2-delta` de cada una). Los `.md` de la carpeta son solo atajo; si difieren, manda el HTML.

1. **Leer** la etapa en `doc-09` (objetivo, requisitos, dependencias, entidades, endpoints, pantallas, pruebas, entregables, riesgos, "Fin") y su `v2-delta`.
2. **Leer** los REQ-xx citados en `doc-01` y las pruebas `T-*` en `doc-08`/`doc-11`; el modelo de las entidades en `doc-04`; los contratos en `doc-05`; permisos en `doc-07`; y las reglas del módulo en `doc-13`. Incluir el `v2-delta` de cada una.

### Paso 2 — Cruzar plan con codebase

Con el snapshot del Paso 0 y los documentos del Paso 1, identificar:

- **Colisiones**: entidades, tablas, endpoints o permisos que el plan pide crear pero que ya existen (total o parcialmente).
- **Patrones a seguir**: módulos similares ya implementados (p. ej., si E5 pide algo análogo a E3, usar E3 como plantilla de la estructura).
- **Dependencias satisfechas**: FK y entidades que la etapa necesita y que ya están en el schema (confirmar con las migraciones del snapshot).
- **Factories de test**: qué IDs sembrados ya existen en `PermissionMatrixWebApplicationFactory` y cuáles habrá que agregar para T-ISO-ALL.

### Paso 3 — Verificar prerrequisitos

- Las etapas de las que depende están cerradas (`git log`, estado del repo confirmado con el snapshot).
- Decisiones D-xx que bloquean la etapa (`doc-10` §1 y §4). Si falta alguna, **parar y preguntar**.
- Cuentas/servicios externos necesarios (email, R2, Turnstile, Docker).

### Paso 4 — Proponer rama(s)

`feature/<etapa>-<tema>` desde `Develop`, una por bloque coherente. Esperar confirmación antes de crearla.

### Paso 5 — Plan de trabajo detallado

Lista ordenada de bloques con:
- **Qué se crea/modifica** (archivos concretos, nombrados)
- **Criterio de terminado** por bloque
- **`T-*` que cubre** (del Paso 1)
- **Diferencias respecto al patrón** del módulo más similar ya implementado (del Paso 2)

Orden estándar: dominio → aplicación → infraestructura → API → migración → tests → UI.

### Paso 6 — Definition of done

= línea "Fin" del roadmap + `dotnet test` y `npm --prefix web run test`/`lint` en verde + `security-reviewer` y `architecture-reviewer` sin hallazgos críticos.

---

**No escribir código hasta que el usuario apruebe el plan de trabajo.**
