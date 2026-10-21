using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TallerControl.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Editar una orden o un presupuesto en borrador reemplaza sus renglones: el rol de la API necesita DELETE sobre
    /// esas dos tablas (el resto de los datos de negocio sigue sin DELETE). Quitar renglones de un documento que
    /// todavía se puede editar no pierde historial: el stock queda en los movimientos y los cambios en la auditoría.
    /// </summary>
    public partial class ItemReplacementDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("GRANT DELETE ON work_order_items, quote_items TO tallercontrol_app;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("REVOKE DELETE ON work_order_items, quote_items FROM tallercontrol_app;");
        }
    }
}
