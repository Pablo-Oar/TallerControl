using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TallerControl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ArcaEnvironment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_homologation",
                table: "invoices",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "environment",
                table: "fiscal_certificates",
                type: "text",
                nullable: false,
                defaultValue: "production");

            migrationBuilder.AddCheckConstraint(
                name: "ck_fiscal_certificates_environment",
                table: "fiscal_certificates",
                sql: "\"environment\" IN ('homologation', 'production')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_fiscal_certificates_environment",
                table: "fiscal_certificates");

            migrationBuilder.DropColumn(
                name: "is_homologation",
                table: "invoices");

            migrationBuilder.DropColumn(
                name: "environment",
                table: "fiscal_certificates");
        }
    }
}
