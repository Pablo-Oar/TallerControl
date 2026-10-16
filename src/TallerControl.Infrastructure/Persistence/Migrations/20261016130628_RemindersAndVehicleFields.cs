using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TallerControl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemindersAndVehicleFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "engine_number",
                table: "vehicles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "usage_profile",
                table: "vehicles",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "reminders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vehicle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    due_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    due_km = table.Column<int>(type: "integer", nullable: true),
                    last_notified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<Guid>(type: "uuid", nullable: true),
                    delete_reason = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reminders", x => x.id);
                    table.CheckConstraint("ck_reminders_kind", "\"kind\" IN ('inspection_due', 'insurance_due', 'other_due', 'service_due')");
                    table.ForeignKey(
                        name: "fk_reminders_tenant_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_reminders_vehicle_vehicle_id",
                        column: x => x.vehicle_id,
                        principalTable: "vehicles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_vehicles_usage_profile",
                table: "vehicles",
                sql: "\"usage_profile\" IN ('commercial', 'low_usage', 'regular')");

            migrationBuilder.CreateIndex(
                name: "ix_reminders_tenant_id_due_date",
                table: "reminders",
                columns: new[] { "tenant_id", "due_date" },
                filter: "deleted_at is null and due_date is not null");

            migrationBuilder.CreateIndex(
                name: "ix_reminders_tenant_id_vehicle_id",
                table: "reminders",
                columns: new[] { "tenant_id", "vehicle_id" },
                filter: "deleted_at is null");

            migrationBuilder.CreateIndex(
                name: "ix_reminders_vehicle_id",
                table: "reminders",
                column: "vehicle_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reminders");

            migrationBuilder.DropCheckConstraint(
                name: "ck_vehicles_usage_profile",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "engine_number",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "usage_profile",
                table: "vehicles");
        }
    }
}
