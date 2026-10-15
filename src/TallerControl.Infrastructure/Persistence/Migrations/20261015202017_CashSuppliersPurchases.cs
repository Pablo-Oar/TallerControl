using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TallerControl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CashSuppliersPurchases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_stock_movements_reason",
                table: "stock_movements");

            migrationBuilder.DropCheckConstraint(
                name: "ck_document_sequences_kind",
                table: "document_sequences");

            migrationBuilder.CreateTable(
                name: "cash_accounts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
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
                    table.PrimaryKey("pk_cash_accounts", x => x.id);
                    table.CheckConstraint("ck_cash_accounts_kind", "\"kind\" IN ('bank_transfer', 'card', 'cash', 'check', 'other')");
                    table.ForeignKey(
                        name: "fk_cash_accounts_branches_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_cash_accounts_tenant_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "counter_sales",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    document_number = table.Column<int>(type: "integer", nullable: false),
                    date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    subtotal_net = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    vat_amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    total = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("pk_counter_sales", x => x.id);
                    table.CheckConstraint("ck_counter_sales_status", "\"status\" IN ('cancelled', 'confirmed', 'draft')");
                    table.ForeignKey(
                        name: "fk_counter_sales_branches_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_counter_sales_customer_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_counter_sales_tenant_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "suppliers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    cuit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    vat_condition = table.Column<string>(type: "text", nullable: false),
                    address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("pk_suppliers", x => x.id);
                    table.CheckConstraint("ck_suppliers_vat_condition", "\"vat_condition\" IN ('consumidor_final', 'exento', 'monotributo', 'no_responsable', 'responsable_inscripto')");
                    table.ForeignKey(
                        name: "fk_suppliers_tenant_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cash_movements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    direction = table.Column<string>(type: "text", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    reference_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    reference_id = table.Column<Guid>(type: "uuid", nullable: true),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_by_membership_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cash_movements", x => x.id);
                    table.CheckConstraint("ck_cash_movements_direction", "\"direction\" IN ('in', 'out')");
                    table.CheckConstraint("ck_cash_movements_reason", "\"reason\" IN ('counter_sale_income', 'manual_expense', 'manual_income', 'payment', 'purchase_payment')");
                    table.ForeignKey(
                        name: "fk_cash_movements_branches_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_cash_movements_cash_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "cash_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_cash_movements_tenant_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cash_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    opened_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    opened_by_membership_id = table.Column<Guid>(type: "uuid", nullable: false),
                    opening_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    closed_by_membership_id = table.Column<Guid>(type: "uuid", nullable: true),
                    closing_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cash_sessions", x => x.id);
                    table.CheckConstraint("ck_cash_sessions_status", "\"status\" IN ('closed', 'open')");
                    table.ForeignKey(
                        name: "fk_cash_sessions_branches_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_cash_sessions_cash_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "cash_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_cash_sessions_tenant_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "counter_sale_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    counter_sale_id = table.Column<Guid>(type: "uuid", nullable: false),
                    catalog_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(12,3)", precision: 14, scale: 2, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    vat_rate = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    net_amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    vat_amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    total = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_counter_sale_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_counter_sale_items_catalog_items_catalog_item_id",
                        column: x => x.catalog_item_id,
                        principalTable: "catalog_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_counter_sale_items_counter_sales_counter_sale_id",
                        column: x => x.counter_sale_id,
                        principalTable: "counter_sales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_counter_sale_items_tenant_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchases",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_number = table.Column<int>(type: "integer", nullable: false),
                    invoice_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    point_of_sale = table.Column<int>(type: "integer", nullable: true),
                    invoice_number = table.Column<int>(type: "integer", nullable: true),
                    subtotal_net = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    vat_amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    perceptions = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false, defaultValue: 0m),
                    retentions = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false, defaultValue: 0m),
                    total = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("pk_purchases", x => x.id);
                    table.CheckConstraint("ck_purchases_status", "\"status\" IN ('cancelled', 'confirmed', 'draft')");
                    table.ForeignKey(
                        name: "fk_purchases_branches_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_purchases_supplier_supplier_id",
                        column: x => x.supplier_id,
                        principalTable: "suppliers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_purchases_tenant_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchase_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_id = table.Column<Guid>(type: "uuid", nullable: false),
                    catalog_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(12,3)", precision: 14, scale: 2, nullable: false),
                    unit_cost = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    vat_rate = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    total = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_purchase_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_purchase_items_catalog_items_catalog_item_id",
                        column: x => x.catalog_item_id,
                        principalTable: "catalog_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_purchase_items_purchases_purchase_id",
                        column: x => x.purchase_id,
                        principalTable: "purchases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_purchase_items_tenant_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            // cash_movements es append-only: el rol de aplicación no puede UPDATE
            migrationBuilder.Sql("REVOKE UPDATE ON cash_movements FROM tallercontrol_app;");

            migrationBuilder.AddCheckConstraint(
                name: "ck_stock_movements_reason",
                table: "stock_movements",
                sql: "\"reason\" IN ('counter_sale', 'counter_sale_cancel', 'item_removed', 'manual_adjust', 'purchase', 'purchase_cancel', 'work_order', 'work_order_cancel')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_document_sequences_kind",
                table: "document_sequences",
                sql: "\"kind\" IN ('cash_session', 'counter_sale', 'payment', 'purchase', 'quote', 'work_order')");

            migrationBuilder.CreateIndex(
                name: "ix_cash_accounts_branch_id",
                table: "cash_accounts",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "ix_cash_accounts_tenant_id_branch_id",
                table: "cash_accounts",
                columns: new[] { "tenant_id", "branch_id" });

            migrationBuilder.CreateIndex(
                name: "ix_cash_movements_account_id",
                table: "cash_movements",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ix_cash_movements_branch_id",
                table: "cash_movements",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "ix_cash_movements_tenant_id_account_id_created_at",
                table: "cash_movements",
                columns: new[] { "tenant_id", "account_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_cash_sessions_account_id",
                table: "cash_sessions",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ix_cash_sessions_branch_id",
                table: "cash_sessions",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "ix_cash_sessions_tenant_id_account_id_status",
                table: "cash_sessions",
                columns: new[] { "tenant_id", "account_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_counter_sale_items_catalog_item_id",
                table: "counter_sale_items",
                column: "catalog_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_counter_sale_items_counter_sale_id",
                table: "counter_sale_items",
                column: "counter_sale_id");

            migrationBuilder.CreateIndex(
                name: "ix_counter_sale_items_tenant_id",
                table: "counter_sale_items",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_counter_sales_branch_id",
                table: "counter_sales",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "ix_counter_sales_customer_id",
                table: "counter_sales",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_counter_sales_tenant_id_branch_id_document_number",
                table: "counter_sales",
                columns: new[] { "tenant_id", "branch_id", "document_number" });

            migrationBuilder.CreateIndex(
                name: "ix_counter_sales_tenant_id_date",
                table: "counter_sales",
                columns: new[] { "tenant_id", "date" });

            migrationBuilder.CreateIndex(
                name: "ix_purchase_items_catalog_item_id",
                table: "purchase_items",
                column: "catalog_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_purchase_items_purchase_id",
                table: "purchase_items",
                column: "purchase_id");

            migrationBuilder.CreateIndex(
                name: "ix_purchase_items_tenant_id",
                table: "purchase_items",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_purchases_branch_id",
                table: "purchases",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "ix_purchases_supplier_id",
                table: "purchases",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "ix_purchases_tenant_id_branch_id_document_number",
                table: "purchases",
                columns: new[] { "tenant_id", "branch_id", "document_number" });

            migrationBuilder.CreateIndex(
                name: "ix_purchases_tenant_id_invoice_date",
                table: "purchases",
                columns: new[] { "tenant_id", "invoice_date" });

            migrationBuilder.CreateIndex(
                name: "ix_suppliers_name_trgm",
                table: "suppliers",
                column: "name")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_suppliers_tenant_id_name",
                table: "suppliers",
                columns: new[] { "tenant_id", "name" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cash_movements");

            migrationBuilder.DropTable(
                name: "cash_sessions");

            migrationBuilder.DropTable(
                name: "counter_sale_items");

            migrationBuilder.DropTable(
                name: "purchase_items");

            migrationBuilder.DropTable(
                name: "cash_accounts");

            migrationBuilder.DropTable(
                name: "counter_sales");

            migrationBuilder.DropTable(
                name: "purchases");

            migrationBuilder.DropTable(
                name: "suppliers");

            migrationBuilder.DropCheckConstraint(
                name: "ck_stock_movements_reason",
                table: "stock_movements");

            migrationBuilder.DropCheckConstraint(
                name: "ck_document_sequences_kind",
                table: "document_sequences");

            migrationBuilder.AddCheckConstraint(
                name: "ck_stock_movements_reason",
                table: "stock_movements",
                sql: "\"reason\" IN ('item_removed', 'manual_adjust', 'work_order', 'work_order_cancel')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_document_sequences_kind",
                table: "document_sequences",
                sql: "\"kind\" IN ('payment', 'quote', 'work_order')");
        }
    }
}
