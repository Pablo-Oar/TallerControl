---
name: resumen-sesion
description: Genera el resumen de cierre de etapa de TallerControl: consolida commits, estado de tests y proyecto, actualiza CLAUDE.md y deja todo listo para que la siguiente sesión arranque con /start-etapa. Ejecutar desde Develop luego de hacer merge del PR de la etapa.
argument-hint: "<etapa recién cerrada, p. ej. E4>"
---

# resumen-sesion

Etapa cerrada: `$ARGUMENTS`

Esta skill se ejecuta **desde `Develop`** justo después de que el usuario hizo `git pull` tras el merge del PR. Su objetivo es dejar un registro preciso del estado del proyecto para que la próxima sesión arranque con contexto completo.

## Pasos

### 1. Verificar punto de partida

```
git branch --show-current        # debe ser Develop
git log --oneline -15            # ver los commits recientes (incluye el merge)
git status                       # debe estar limpio
```

Si la rama no es `Develop` o hay cambios sin commitear, **parar y avisar**.

### 2. Snapshot del codebase (subagente Explore)

Lanzar el agente `Explore` con la siguiente consigna (en modo `run_in_background: false` porque el resumen depende de sus hallazgos):

> "Hace un relevamiento completo del codebase de TallerControl para un resumen de cierre de etapa. Necesito los siguientes datos estructurados, sin analizar calidad ni hacer sugerencias — solo inventario:
>
> 1. **Módulos implementados**: para cada subcarpeta de `src/TallerControl.Domain/`, `src/TallerControl.Application/` y `src/TallerControl.Infrastructure/`, lista el nombre y los archivos `.cs` que contiene (solo nombres, no contenido).
> 2. **Endpoints registrados**: busca todos los archivos `*Endpoints.cs` en `src/TallerControl.Api/Endpoints/` y lista el nombre de cada archivo.
> 3. **Migraciones**: lista todos los archivos `*.cs` en `src/TallerControl.Infrastructure/Persistence/Migrations/` que NO sean `*.Designer.cs` ni `AppDbContextModelSnapshot.cs`, en orden cronológico.
> 4. **Tests**: cuenta los archivos `*Tests.cs` en `tests/TallerControl.Domain.Tests/`, `tests/TallerControl.Application.Tests/` y `tests/TallerControl.Api.IntegrationTests/` por carpeta.
> 5. **Pantallas React**: lista todos los archivos `.tsx` en `web/src/areas/` agrupados por subcarpeta.
> 6. **Archivos de API client**: lista los archivos `.ts` en `web/src/lib/api/`.
>
> Devuelve un informe estructurado con esas 6 secciones. Nada más."

Esperar el resultado del subagente antes de continuar.

### 3. Contar tests reales

Ejecutar `dotnet test TallerControl.slnx --no-build --list-tests 2>&1 | Measure-Object -Line` para obtener el total actual (o simplemente `dotnet test TallerControl.slnx --no-build 2>&1 | Select-Object -Last 5` para ver el resumen).

Ejecutar también `npm --prefix web run test 2>&1 | Select-Object -Last 5`.

### 4. Leer CLAUDE.md actual

Leer el archivo `CLAUDE.md` para identificar la sección "Estado actual" y las secciones de cada etapa implementada. Esto sirve de base para la actualización.

### 5. Redactar el resumen de sesión

Con los datos de los pasos 2 y 3, redactar el resumen con la siguiente estructura:

---

## Resumen de sesión — Etapa $ARGUMENTS (fecha: hoy)

### ¿Qué se hizo?
- Lista de los commits del PR (del `git log`)
- Módulos nuevos o modificados
- Decisiones tomadas que se desvíen del plan (o "ninguna")

### Estado de tests
- `dotnet test`: N passing (desglose Domain/Application/Integration)
- `npm test`: N passing

### Módulos implementados (acumulado)
(Tabla: Módulo | Entidades | Endpoints | Tests de integración)

### Pantallas React implementadas
(Lista por área)

### Migraciones aplicadas
(Lista en orden)

### Decisiones abiertas relevantes para la próxima etapa
(De `D-xx` del plan que bloqueen o afecten la siguiente etapa)

### Próximo paso
**Siguiente etapa según roadmap**: EXX — [nombre]
Ejecutar `/start-etapa EXX` para iniciar la planificación.

---

### 6. Actualizar CLAUDE.md

Actualizar la sección **"Estado actual"** del `CLAUDE.md` con:
- Etapas completadas (agregar la recién cerrada como "completa, mergeada a Develop")
- Sección nueva `**EX — lo implementado**` con el mismo nivel de detalle que las secciones E2/E3/E4 existentes
- Actualizar la línea de pendientes ("E3 completa...; sigue E4" → ajustar a la nueva etapa)

Solo tocar la sección de estado del proyecto; no modificar reglas de arquitectura, stack, ni convenciones.

### 7. Informar al usuario

Mostrar el resumen del paso 5 en la respuesta.
Al final, indicar claramente:

> **Para iniciar la próxima etapa**, ejecutar: `/start-etapa EXX`
