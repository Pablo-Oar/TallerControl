using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TallerControl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class QrCardsAndVerifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "owner_verifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    qr_card_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    otp_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    consumed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_owner_verifications", x => x.id);
                    table.ForeignKey(
                        name: "fk_owner_verifications_tenant_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "qr_cards",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    vehicle_id = table.Column<Guid>(type: "uuid", nullable: true),
                    assigned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoke_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_qr_cards", x => x.id);
                    table.CheckConstraint("ck_qr_cards_status", "\"status\" IN ('assigned', 'free', 'lost', 'revoked')");
                    table.ForeignKey(
                        name: "fk_qr_cards_tenant_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_qr_cards_vehicle_vehicle_id",
                        column: x => x.vehicle_id,
                        principalTable: "vehicles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "qr_scan_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    qr_card_id = table.Column<Guid>(type: "uuid", nullable: true),
                    code_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    event_type = table.Column<string>(type: "text", nullable: false),
                    outcome = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ip_truncated = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    user_agent = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_qr_scan_events", x => x.id);
                    table.CheckConstraint("ck_qr_scan_events_event_type", "\"event_type\" IN ('otp_failed', 'otp_sent', 'owner_challenge', 'owner_session', 'scan')");
                });

            migrationBuilder.CreateIndex(
                name: "ix_owner_verifications_card_expires",
                table: "owner_verifications",
                columns: new[] { "qr_card_id", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "ix_owner_verifications_tenant_id",
                table: "owner_verifications",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_qr_cards_code",
                table: "qr_cards",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_qr_cards_tenant_id_status",
                table: "qr_cards",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_qr_cards_tenant_vehicle_active",
                table: "qr_cards",
                columns: new[] { "tenant_id", "vehicle_id" },
                unique: true,
                filter: "status IN ('assigned', 'lost') AND vehicle_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_qr_cards_vehicle_id",
                table: "qr_cards",
                column: "vehicle_id");

            migrationBuilder.CreateIndex(
                name: "ix_qr_scan_events_tenant_card_at",
                table: "qr_scan_events",
                columns: new[] { "tenant_id", "qr_card_id", "created_at" });

            // qr_scan_events es append-only: el rol app no puede modificar ni borrar eventos de escaneo.
            migrationBuilder.Sql("REVOKE UPDATE, DELETE ON qr_scan_events FROM tallercontrol_app;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "owner_verifications");

            migrationBuilder.DropTable(
                name: "qr_cards");

            migrationBuilder.DropTable(
                name: "qr_scan_events");
        }
    }
}
