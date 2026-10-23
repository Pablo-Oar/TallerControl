using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TallerControl.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Los horarios y días bloqueados de la agenda son configuración, no datos de negocio con historial: quitarlos tiene
    /// que poder borrarlos (sin este permiso «Quitar» respondía 500).
    /// </summary>
    public partial class AvailabilityDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("GRANT DELETE ON availability_rules, availability_exceptions TO tallercontrol_app;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("REVOKE DELETE ON availability_rules, availability_exceptions FROM tallercontrol_app;");
        }
    }
}
