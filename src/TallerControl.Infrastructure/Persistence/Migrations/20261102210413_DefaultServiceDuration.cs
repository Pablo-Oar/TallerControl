using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TallerControl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DefaultServiceDuration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "default_service_minutes",
                table: "tenant_settings",
                type: "integer",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.AddCheckConstraint(
                name: "ck_tenant_settings_default_service_minutes",
                table: "tenant_settings",
                sql: "default_service_minutes BETWEEN 5 AND 480");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_tenant_settings_default_service_minutes",
                table: "tenant_settings");

            migrationBuilder.DropColumn(
                name: "default_service_minutes",
                table: "tenant_settings");
        }
    }
}
