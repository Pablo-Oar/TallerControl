---
name: ef-migration
description: Crea y revisa migraciones de EF Core 10 / PostgreSQL 18 para TallerControl según las convenciones del modelo de datos (snake_case, uuid v7, tenant_id, numeric, timestamptz, CHECK en enums, xmin). Usar al cambiar entidades o su configuración.
disable-model-invocation: true
argument-hint: "<NombreDeLaMigracion>"
---

# ef-migration

Migración: `$ARGUMENTS`

## Antes de generar

- Las convenciones están en `04_modelo_datos.md` §1. Verificá la configuración de la entidad contra ellas:
  - tablas/columnas `snake_case` en inglés;
  - PK `uuid` (v7 desde la aplicación);
  - `tenant_id not null` + FK a `tenants` + índices compuestos que **empiezan** por `tenant_id`;
  - dinero `numeric(14,2)`, tasas `numeric(5,2)`, cantidades `numeric(12,3)`;
  - fechas `timestamptz`; emails `citext`;
  - enums como `text` + `CHECK (... in (...))`;
  - `xmin` como concurrency token en entidades editables;
  - FK con `ON DELETE RESTRICT`; sin borrado físico.

- **Cambios de modelo V2** (`V2/14_cambios_por_documento_v2.md`, sección 04): `document_sequences` con `branch_id` en la clave, `stock_movements.branch_id`, `payments.account_id` nulo, `catalog_items.buffer_minutes`, `vehicles.engine_number` y `usage_profile`. Verificá que la migración los incluya.

## Generar

Pedir confirmación al usuario antes de ejecutar (el proyecto de migraciones es `Infrastructure`, el de arranque `Api`):

```
dotnet ef migrations add <Nombre> --project src/TallerControl.Infrastructure --startup-project src/TallerControl.Api
```

## Revisar el archivo generado

- El SQL coincide con lo esperado; nada de `DROP` inesperado ni pérdida de datos.
- Se crean los índices únicos parciales y los `CHECK` previstos.
- `Down()` es coherente.
- Una migración ya aplicada en staging/prod **no se edita**: se crea otra nueva.
- No aplicar (`database update`) sin confirmación del usuario.

Validar con una prueba de integración que las migraciones aplican desde cero sobre PostgreSQL real (Testcontainers).
