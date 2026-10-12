# TallerControl

SaaS de gestión para talleres mecánicos: vehículos y su historial, el ciclo presupuesto → orden → cobro, portal del mecánico y tarjetas QR seguras para el dueño.
Alternativa propia a Vehix, multiempresa y con API JSON reutilizable.

## Stack

- **Backend:** ASP.NET Core 10 Web API (C#), EF Core 10 + Npgsql
- **Frontend:** React + TypeScript (Vite)
- **Base de datos:** PostgreSQL 18
- **Archivos:** Cloudflare R2 (RustFS, compatible con S3, en desarrollo local)

## Entorno local

Requisitos: Docker Desktop (con WSL 2), .NET SDK 10 y Node.js 22.

```powershell
copy .env.example .env      # cambiar las contraseñas locales
docker compose up -d        # PostgreSQL 18 + almacenamiento S3 local
docker compose ps           # db y storage deben figurar como "healthy"
docker compose down         # apagar; los datos se conservan (down -v los borra)
```

| Servicio | Dirección local | Notas |
|---|---|---|
| PostgreSQL 18 | `127.0.0.1:5432` | Usuario y base: `tallercontrol` (ver `.env`) |
| API S3 | `http://127.0.0.1:9000` | Bucket `tallercontrol-dev`, creado al levantar |
| Consola del almacenamiento | `http://127.0.0.1:9001` | Misma clave de acceso que la API |

Los puertos solo escuchan en `127.0.0.1`. El almacenamiento local es un sustituto de desarrollo: MinIO dejó de publicar imágenes y se usa RustFS; CORS y URL prefirmadas se validan contra el bucket real de staging.

## Documentación

El plan de implementación (alcance, decisiones, roadmap) es documentación interna y no se versiona en este repositorio.
Las convenciones de trabajo del equipo están en [`CLAUDE.md`](CLAUDE.md).

## Estado

Etapa **E0** — preparación del repositorio y estructura.

## Licencia

Software propietario. Todos los derechos reservados. Ver [LICENSE](LICENSE).
