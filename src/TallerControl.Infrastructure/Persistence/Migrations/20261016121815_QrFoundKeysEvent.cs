using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TallerControl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class QrFoundKeysEvent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_qr_scan_events_event_type",
                table: "qr_scan_events");

            migrationBuilder.AddCheckConstraint(
                name: "ck_qr_scan_events_event_type",
                table: "qr_scan_events",
                sql: "\"event_type\" IN ('found_keys', 'otp_failed', 'otp_sent', 'owner_challenge', 'owner_session', 'scan')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_qr_scan_events_event_type",
                table: "qr_scan_events");

            migrationBuilder.AddCheckConstraint(
                name: "ck_qr_scan_events_event_type",
                table: "qr_scan_events",
                sql: "\"event_type\" IN ('otp_failed', 'otp_sent', 'owner_challenge', 'owner_session', 'scan')");
        }
    }
}
