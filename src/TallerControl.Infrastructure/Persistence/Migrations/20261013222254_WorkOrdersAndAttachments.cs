using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TallerControl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WorkOrdersAndAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "attachments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_type = table.Column<string>(type: "text", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    is_internal = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_attachments", x => x.id);
                    table.CheckConstraint("ck_attachments_owner_type", "\"owner_type\" IN ('quote', 'vehicle', 'work_order')");
                    table.CheckConstraint("ck_attachments_status", "\"status\" IN ('pending_upload', 'ready')");
                    table.ForeignKey(
                        name: "fk_attachments_tenant_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "work_orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vehicle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_mechanic_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    priority = table.Column<string>(type: "text", nullable: false),
                    odometer_km = table.Column<int>(type: "integer", nullable: false),
                    fuel_level = table.Column<int>(type: "integer", nullable: true),
                    reported_issue = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    mechanic_notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    internal_notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    source_quote_id = table.Column<Guid>(type: "uuid", nullable: true),
                    subtotal_net = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    discount_amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    vat_amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    total = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    paid_amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    delivered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancel_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    balance = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false, computedColumnSql: "CASE WHEN status = 'cancelled' THEN 0::numeric ELSE total - paid_amount END", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_work_orders", x => x.id);
                    table.CheckConstraint("ck_work_orders_priority", "\"priority\" IN ('high', 'low', 'normal')");
                    table.CheckConstraint("ck_work_orders_status", "\"status\" IN ('cancelled', 'completed', 'delivered', 'in_progress', 'pending', 'waiting_parts')");
                    table.ForeignKey(
                        name: "fk_work_orders_branches_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_work_orders_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_work_orders_memberships_assigned_mechanic_id",
                        column: x => x.assigned_mechanic_id,
                        principalTable: "memberships",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_work_orders_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_work_orders_vehicles_vehicle_id",
                        column: x => x.vehicle_id,
                        principalTable: "vehicles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "work_order_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    work_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    catalog_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    quantity = table.Column<decimal>(type: "numeric(12,3)", precision: 14, scale: 2, nullable: false),
                    unit_net_price = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    vat_rate = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    line_net = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    line_vat = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    line_total = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    unit_cost_snapshot = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    position = table.Column<int>(type: "integer", nullable: false),
                    quote_item_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_work_order_items", x => x.id);
                    table.CheckConstraint("ck_work_order_items_kind", "\"kind\" IN ('manual', 'product', 'service')");
                    table.ForeignKey(
                        name: "fk_work_order_items_catalog_items_catalog_item_id",
                        column: x => x.catalog_item_id,
                        principalTable: "catalog_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_work_order_items_work_orders_work_order_id",
                        column: x => x.work_order_id,
                        principalTable: "work_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "work_order_status_changes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    work_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_status = table.Column<string>(type: "text", nullable: true),
                    to_status = table.Column<string>(type: "text", nullable: false),
                    note = table.Column<string>(type: "text", nullable: true),
                    changed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_work_order_status_changes", x => x.id);
                    table.CheckConstraint("ck_work_order_status_changes_from_status", "\"from_status\" IN ('cancelled', 'completed', 'delivered', 'in_progress', 'pending', 'waiting_parts')");
                    table.CheckConstraint("ck_work_order_status_changes_to_status", "\"to_status\" IN ('cancelled', 'completed', 'delivered', 'in_progress', 'pending', 'waiting_parts')");
                    table.ForeignKey(
                        name: "fk_work_order_status_changes_work_orders_work_order_id",
                        column: x => x.work_order_id,
                        principalTable: "work_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_attachments_tenant_owner",
                table: "attachments",
                columns: new[] { "tenant_id", "owner_type", "owner_id" });

            migrationBuilder.CreateIndex(
                name: "ix_work_order_items_catalog_item_id",
                table: "work_order_items",
                column: "catalog_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_work_order_items_tenant_order",
                table: "work_order_items",
                columns: new[] { "tenant_id", "work_order_id" });

            migrationBuilder.CreateIndex(
                name: "ix_work_order_items_work_order_id",
                table: "work_order_items",
                column: "work_order_id");

            migrationBuilder.CreateIndex(
                name: "ix_work_order_status_changes_tenant_order",
                table: "work_order_status_changes",
                columns: new[] { "tenant_id", "work_order_id" });

            migrationBuilder.CreateIndex(
                name: "ix_work_order_status_changes_work_order_id",
                table: "work_order_status_changes",
                column: "work_order_id");

            migrationBuilder.CreateIndex(
                name: "ix_work_orders_assigned_mechanic_id",
                table: "work_orders",
                column: "assigned_mechanic_id");

            migrationBuilder.CreateIndex(
                name: "ix_work_orders_branch_id",
                table: "work_orders",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "ix_work_orders_customer_id",
                table: "work_orders",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_work_orders_tenant_branch_number",
                table: "work_orders",
                columns: new[] { "tenant_id", "branch_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_work_orders_tenant_customer",
                table: "work_orders",
                columns: new[] { "tenant_id", "customer_id" });

            migrationBuilder.CreateIndex(
                name: "ix_work_orders_tenant_mechanic",
                table: "work_orders",
                columns: new[] { "tenant_id", "assigned_mechanic_id" });

            migrationBuilder.CreateIndex(
                name: "ix_work_orders_tenant_status",
                table: "work_orders",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_work_orders_tenant_vehicle",
                table: "work_orders",
                columns: new[] { "tenant_id", "vehicle_id" });

            migrationBuilder.CreateIndex(
                name: "ix_work_orders_vehicle_id",
                table: "work_orders",
                column: "vehicle_id");

            // work_order_status_changes es append-only: el rol de aplicación no puede modificar ni borrar.
            migrationBuilder.Sql(
                "REVOKE UPDATE, DELETE ON work_order_status_changes FROM tallercontrol_app;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "attachments");

            migrationBuilder.DropTable(
                name: "work_order_items");

            migrationBuilder.DropTable(
                name: "work_order_status_changes");

            migrationBuilder.DropTable(
                name: "work_orders");
        }
    }
}
