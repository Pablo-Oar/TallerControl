using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TallerControl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DashboardThresholds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "aged_receivable_days",
                table: "tenant_settings",
                type: "integer",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.AddColumn<int>(
                name: "hot_quote_hours",
                table: "tenant_settings",
                type: "integer",
                nullable: false,
                defaultValue: 48);

            migrationBuilder.AddColumn<int>(
                name: "stuck_order_days",
                table: "tenant_settings",
                type: "integer",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.AddCheckConstraint(
                name: "ck_tenant_settings_aged_receivable_days",
                table: "tenant_settings",
                sql: "aged_receivable_days BETWEEN 1 AND 365");

            migrationBuilder.AddCheckConstraint(
                name: "ck_tenant_settings_hot_quote_hours",
                table: "tenant_settings",
                sql: "hot_quote_hours BETWEEN 1 AND 720");

            migrationBuilder.AddCheckConstraint(
                name: "ck_tenant_settings_stuck_order_days",
                table: "tenant_settings",
                sql: "stuck_order_days BETWEEN 1 AND 60");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_tenant_settings_aged_receivable_days",
                table: "tenant_settings");

            migrationBuilder.DropCheckConstraint(
                name: "ck_tenant_settings_hot_quote_hours",
                table: "tenant_settings");

            migrationBuilder.DropCheckConstraint(
                name: "ck_tenant_settings_stuck_order_days",
                table: "tenant_settings");

            migrationBuilder.DropColumn(
                name: "aged_receivable_days",
                table: "tenant_settings");

            migrationBuilder.DropColumn(
                name: "hot_quote_hours",
                table: "tenant_settings");

            migrationBuilder.DropColumn(
                name: "stuck_order_days",
                table: "tenant_settings");
        }
    }
}
