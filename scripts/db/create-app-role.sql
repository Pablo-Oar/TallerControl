-- Crea el rol de la aplicacion (o actualiza su clave). Es idempotente.
-- Lo corre scripts/dev.ps1 antes de las migraciones; en otros entornos se corre una vez
-- con un rol que pueda crear roles:
--   psql -v ON_ERROR_STOP=1 -v app_password=<clave> -d <base> -f create-app-role.sql
--
-- El rol no es superusuario ni dueno de nada: los permisos sobre las tablas los otorga la
-- migracion AppRolePermissions. La aplicacion se conecta con este rol; las migraciones
-- (dotnet ef / bundle) usan el dueno de la base.

SELECT format('CREATE ROLE tallercontrol_app LOGIN PASSWORD %L', :'app_password')
WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'tallercontrol_app')
\gexec

SELECT format('ALTER ROLE tallercontrol_app WITH LOGIN PASSWORD %L', :'app_password')
\gexec

SELECT format('GRANT CONNECT ON DATABASE %I TO tallercontrol_app', current_database())
\gexec
