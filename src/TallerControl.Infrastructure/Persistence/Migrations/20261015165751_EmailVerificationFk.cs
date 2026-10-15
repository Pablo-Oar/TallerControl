using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TallerControl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EmailVerificationFk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_email_verification_tokens_users_user_id",
                table: "email_verification_tokens");

            migrationBuilder.CreateIndex(
                name: "ix_email_verification_tokens_user_id",
                table: "email_verification_tokens",
                column: "user_id");

            migrationBuilder.AddForeignKey(
                name: "fk_email_verification_tokens_user_user_id",
                table: "email_verification_tokens",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_email_verification_tokens_user_user_id",
                table: "email_verification_tokens");

            migrationBuilder.DropIndex(
                name: "ix_email_verification_tokens_user_id",
                table: "email_verification_tokens");

            migrationBuilder.AddForeignKey(
                name: "fk_email_verification_tokens_users_user_id",
                table: "email_verification_tokens",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
