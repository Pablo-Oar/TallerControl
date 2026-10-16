using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TallerControl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AppointmentsAndAvailability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "appointments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vehicle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mechanic_membership_id = table.Column<Guid>(type: "uuid", nullable: true),
                    scheduled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    total_duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    cancellation_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    confirmation_token_hash = table.Column<byte[]>(type: "bytea", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_appointments", x => x.id);
                    table.CheckConstraint("ck_appointments_status", "\"status\" IN ('cancelled', 'completed', 'confirmed', 'pending')");
                    table.ForeignKey(
                        name: "fk_appointments_customer_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_appointments_tenant_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_appointments_vehicle_vehicle_id",
                        column: x => x.vehicle_id,
                        principalTable: "vehicles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "availability_exceptions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mechanic_membership_id = table.Column<Guid>(type: "uuid", nullable: true),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    is_blocked = table.Column<bool>(type: "boolean", nullable: false),
                    start_time = table.Column<TimeOnly>(type: "time", nullable: true),
                    end_time = table.Column<TimeOnly>(type: "time", nullable: true),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_availability_exceptions", x => x.id);
                    table.ForeignKey(
                        name: "fk_availability_exceptions_membership_mechanic_membership_id",
                        column: x => x.mechanic_membership_id,
                        principalTable: "memberships",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_availability_exceptions_tenant_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "availability_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mechanic_membership_id = table.Column<Guid>(type: "uuid", nullable: false),
                    day_of_week = table.Column<string>(type: "text", nullable: false),
                    start_time = table.Column<TimeOnly>(type: "time", nullable: false),
                    end_time = table.Column<TimeOnly>(type: "time", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_availability_rules", x => x.id);
                    table.CheckConstraint("ck_availability_rules_day_of_week", "\"day_of_week\" IN ('friday', 'monday', 'saturday', 'sunday', 'thursday', 'tuesday', 'wednesday')");
                    table.ForeignKey(
                        name: "fk_availability_rules_membership_mechanic_membership_id",
                        column: x => x.mechanic_membership_id,
                        principalTable: "memberships",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_availability_rules_tenant_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "appointment_services",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    appointment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    catalog_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    buffer_minutes = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_appointment_services", x => x.id);
                    table.ForeignKey(
                        name: "fk_appointment_services_appointments_appointment_id",
                        column: x => x.appointment_id,
                        principalTable: "appointments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_appointment_services_catalog_item_catalog_item_id",
                        column: x => x.catalog_item_id,
                        principalTable: "catalog_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_appointment_services_appointment_id",
                table: "appointment_services",
                column: "appointment_id");

            migrationBuilder.CreateIndex(
                name: "ix_appointment_services_catalog_item_id",
                table: "appointment_services",
                column: "catalog_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_appointments_confirmation_token_hash",
                table: "appointments",
                column: "confirmation_token_hash",
                filter: "confirmation_token_hash IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_appointments_customer_id",
                table: "appointments",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_appointments_tenant_id_vehicle_id_scheduled_at",
                table: "appointments",
                columns: new[] { "tenant_id", "vehicle_id", "scheduled_at" },
                unique: true,
                filter: "status NOT IN ('cancelled', 'completed')");

            migrationBuilder.CreateIndex(
                name: "ix_appointments_vehicle_id",
                table: "appointments",
                column: "vehicle_id");

            migrationBuilder.CreateIndex(
                name: "ix_availability_exceptions_mechanic_membership_id",
                table: "availability_exceptions",
                column: "mechanic_membership_id");

            migrationBuilder.CreateIndex(
                name: "ix_availability_exceptions_tenant_id_date",
                table: "availability_exceptions",
                columns: new[] { "tenant_id", "date" });

            migrationBuilder.CreateIndex(
                name: "ix_availability_rules_mechanic_membership_id",
                table: "availability_rules",
                column: "mechanic_membership_id");

            migrationBuilder.CreateIndex(
                name: "ix_availability_rules_tenant_id_mechanic_membership_id_day_of_",
                table: "availability_rules",
                columns: new[] { "tenant_id", "mechanic_membership_id", "day_of_week" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "appointment_services");

            migrationBuilder.DropTable(
                name: "availability_exceptions");

            migrationBuilder.DropTable(
                name: "availability_rules");

            migrationBuilder.DropTable(
                name: "appointments");
        }
    }
}
