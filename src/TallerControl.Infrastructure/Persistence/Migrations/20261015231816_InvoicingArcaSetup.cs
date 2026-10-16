using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TallerControl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InvoicingArcaSetup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_memberships_role",
                table: "memberships");

            migrationBuilder.DropCheckConstraint(
                name: "ck_invitations_role",
                table: "invitations");

            migrationBuilder.CreateTable(
                name: "fiscal_certificates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cuit = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false),
                    private_key_encrypted = table.Column<byte[]>(type: "bytea", nullable: false),
                    csr_pem = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    certificate_pem = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fiscal_certificates", x => x.id);
                    table.CheckConstraint("ck_fiscal_certificates_status", "\"status\" IN ('active', 'expired', 'pending', 'revoked')");
                    table.ForeignKey(
                        name: "fk_fiscal_certificates_tenant_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "fiscal_points_of_sale",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fiscal_certificate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fiscal_points_of_sale", x => x.id);
                    table.ForeignKey(
                        name: "fk_fiscal_points_of_sale_fiscal_certificates_fiscal_certificat",
                        column: x => x.fiscal_certificate_id,
                        principalTable: "fiscal_certificates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_fiscal_points_of_sale_tenant_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "invoices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fiscal_point_of_sale_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_type = table.Column<string>(type: "text", nullable: false),
                    source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    invoice_type = table.Column<string>(type: "text", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: true),
                    cae = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    cae_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    is_credit = table.Column<bool>(type: "boolean", nullable: false),
                    original_invoice_id = table.Column<Guid>(type: "uuid", nullable: true),
                    net_amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    vat_amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    total_amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    issued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    arca_response = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    rejection_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_invoices", x => x.id);
                    table.CheckConstraint("ck_invoices_invoice_type", "\"invoice_type\" IN ('a', 'b', 'c', 'm')");
                    table.CheckConstraint("ck_invoices_source_type", "\"source_type\" IN ('counter_sale', 'work_order')");
                    table.CheckConstraint("ck_invoices_status", "\"status\" IN ('authorized', 'pending', 'rejected')");
                    table.ForeignKey(
                        name: "fk_invoices_fiscal_points_of_sale_fiscal_point_of_sale_id",
                        column: x => x.fiscal_point_of_sale_id,
                        principalTable: "fiscal_points_of_sale",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_invoices_invoices_original_invoice_id",
                        column: x => x.original_invoice_id,
                        principalTable: "invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_invoices_tenant_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_memberships_role",
                table: "memberships",
                sql: "\"role\" IN ('accountant', 'admin_staff', 'mechanic', 'owner')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_invitations_role",
                table: "invitations",
                sql: "\"role\" IN ('accountant', 'admin_staff', 'mechanic', 'owner')");

            migrationBuilder.CreateIndex(
                name: "ix_fiscal_certificates_tenant_active",
                table: "fiscal_certificates",
                column: "tenant_id",
                filter: "status = 'active'");

            migrationBuilder.CreateIndex(
                name: "ix_fiscal_certificates_tenant_cuit",
                table: "fiscal_certificates",
                columns: new[] { "tenant_id", "cuit" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_fiscal_points_of_sale_fiscal_certificate_id",
                table: "fiscal_points_of_sale",
                column: "fiscal_certificate_id");

            migrationBuilder.CreateIndex(
                name: "ix_fiscal_points_of_sale_tenant_number",
                table: "fiscal_points_of_sale",
                columns: new[] { "tenant_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_invoices_fiscal_point_of_sale_id",
                table: "invoices",
                column: "fiscal_point_of_sale_id");

            migrationBuilder.CreateIndex(
                name: "ix_invoices_original_invoice_id",
                table: "invoices",
                column: "original_invoice_id");

            migrationBuilder.CreateIndex(
                name: "ix_invoices_tenant_issued_at",
                table: "invoices",
                columns: new[] { "tenant_id", "issued_at" });

            migrationBuilder.CreateIndex(
                name: "ix_invoices_tenant_source",
                table: "invoices",
                columns: new[] { "tenant_id", "source_type", "source_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "invoices");

            migrationBuilder.DropTable(
                name: "fiscal_points_of_sale");

            migrationBuilder.DropTable(
                name: "fiscal_certificates");

            migrationBuilder.DropCheckConstraint(
                name: "ck_memberships_role",
                table: "memberships");

            migrationBuilder.DropCheckConstraint(
                name: "ck_invitations_role",
                table: "invitations");

            migrationBuilder.AddCheckConstraint(
                name: "ck_memberships_role",
                table: "memberships",
                sql: "\"role\" IN ('admin_staff', 'mechanic', 'owner')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_invitations_role",
                table: "invitations",
                sql: "\"role\" IN ('admin_staff', 'mechanic', 'owner')");
        }
    }
}
