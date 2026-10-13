using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TallerControl.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Permisos del rol de la aplicación (<c>tallercontrol_app</c>, ver scripts/db/create-app-role.sql).
    /// Solo SELECT, INSERT y UPDATE: el borrado físico está prohibido (doc 04 §1) y
    /// <c>audit_events</c> no admite UPDATE (REQ-19). Si el rol no existe todavía no se hace nada;
    /// hay que crearlo y volver a aplicar esta migración antes de usar la aplicación.
    /// Una tabla futura de solo inserción debe revocar UPDATE en su propia migración.
    /// </summary>
    public partial class AppRolePermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'tallercontrol_app') THEN
                        GRANT USAGE ON SCHEMA public TO tallercontrol_app;
                        GRANT SELECT, INSERT, UPDATE ON ALL TABLES IN SCHEMA public TO tallercontrol_app;

                        -- Excepciones: el historial de migraciones es del dueño y la auditoría es de solo inserción.
                        REVOKE ALL ON TABLE public.""__EFMigrationsHistory"" FROM tallercontrol_app;
                        REVOKE UPDATE ON TABLE public.audit_events FROM tallercontrol_app;

                        -- Tablas que cree el dueño en migraciones futuras.
                        ALTER DEFAULT PRIVILEGES IN SCHEMA public
                            GRANT SELECT, INSERT, UPDATE ON TABLES TO tallercontrol_app;
                    END IF;
                END
                $$;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'tallercontrol_app') THEN
                        ALTER DEFAULT PRIVILEGES IN SCHEMA public
                            REVOKE SELECT, INSERT, UPDATE ON TABLES FROM tallercontrol_app;
                        REVOKE ALL ON ALL TABLES IN SCHEMA public FROM tallercontrol_app;
                        REVOKE USAGE ON SCHEMA public FROM tallercontrol_app;
                    END IF;
                END
                $$;");
        }
    }
}