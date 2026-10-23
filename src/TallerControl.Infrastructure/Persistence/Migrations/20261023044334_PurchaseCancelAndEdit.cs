using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TallerControl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PurchaseCancelAndEdit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Editar una compra en borrador reemplaza sus renglones: el rol de la API necesita DELETE sobre purchase_items
            // (solo de un documento todavía editable; el historial de lo confirmado no se toca).
            migrationBuilder.Sql("GRANT DELETE ON purchase_items TO tallercontrol_app;");

            migrationBuilder.AddColumn<string>(
                name: "cancel_reason",
                table: "purchases",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "cancelled_at",
                table: "purchases",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("REVOKE DELETE ON purchase_items FROM tallercontrol_app;");

            migrationBuilder.DropColumn(
                name: "cancel_reason",
                table: "purchases");

            migrationBuilder.DropColumn(
                name: "cancelled_at",
                table: "purchases");
        }
    }
}
