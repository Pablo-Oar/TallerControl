using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TallerControl.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Reabrir un presupuesto (o que el cliente responda de nuevo) marca como reemplazada la respuesta vigente
    /// (<c>is_current = false</c>), y el rol de la API no tenía UPDATE sobre <c>quote_responses</c> (500). Se concede
    /// UPDATE solo sobre esa columna: el contenido de la respuesta (decisión, firma, documento) sigue siendo inmutable.
    /// </summary>
    public partial class QuoteResponseSupersedeGrant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("GRANT UPDATE (is_current) ON quote_responses TO tallercontrol_app;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("REVOKE UPDATE (is_current) ON quote_responses FROM tallercontrol_app;");
        }
    }
}
