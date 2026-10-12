---
name: new-module
description: Crea el esqueleto de un módulo de TallerControl (Domain, Application, Infrastructure, Api, pruebas) siguiendo la arquitectura de monolito modular del plan. Usar al empezar un módulo como Customers, Vehicles, Catalog, Work, Payments o Qr.
disable-model-invocation: true
argument-hint: "<nombre del módulo, p. ej. Customers>"
---

# new-module

Módulo: `$ARGUMENTS`

1. Confirmá en `03_arquitectura_propuesta.md` (tabla de componentes y "Dependencias permitidas") sus entidades, prioridad y a qué módulos puede depender. Si no está o es P1+, avisá.
2. Confirmá tablas y columnas en `04_modelo_datos.md`.

## Estructura a crear

```
src/TallerControl.Domain/<Modulo>/            entidades, value objects, reglas (sin EF)
src/TallerControl.Application/<Modulo>/       casos de uso (<Accion>Handler), DTO, interfaces del módulo
src/TallerControl.Infrastructure/<Modulo>/    configuración EF (IEntityTypeConfiguration), repos si hacen falta
src/TallerControl.Api/Endpoints/<Modulo>/     mapeo de endpoints (finos)
tests/TallerControl.Domain.Tests/<Modulo>/
tests/TallerControl.Application.Tests/<Modulo>/
tests/TallerControl.Api.IntegrationTests/<Modulo>/
```

## Reglas al generar

- Entidades con comportamiento; constructores/factories que garanticen invariantes; setters privados.
- Entidad del taller → `TenantId`, `HasQueryFilter`, índices que empiezan por `tenant_id`, campos de auditoría básica, `xmin` si es editable, borrado lógico solo si `04` lo indica.
- Registrar el módulo con una extensión `Add<Modulo>(this IServiceCollection)`.
- Exponer a otros módulos solo interfaces de `Application`; nada de `DbContext` compartido.
- Migración con la skill `ef-migration`; endpoints con `new-endpoint`.
- Sin MediatR ni AutoMapper.

Al final, ejecutá `dotnet build` y reportá el resultado real.
