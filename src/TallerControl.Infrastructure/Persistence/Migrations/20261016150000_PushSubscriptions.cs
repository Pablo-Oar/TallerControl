using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TallerControl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PushSubscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE TABLE push_subscriptions (
                    user_id    uuid NOT NULL,
                    endpoint   text NOT NULL,
                    p256dh     text NOT NULL,
                    auth       text NOT NULL,
                    created_at timestamptz NOT NULL DEFAULT now(),
                    CONSTRAINT pk_push_subscriptions PRIMARY KEY (user_id, endpoint),
                    CONSTRAINT fk_push_subscriptions_users
                        FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
                );
                CREATE INDEX ix_push_subscriptions_user_id ON push_subscriptions (user_id);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS push_subscriptions;");
        }
    }
}
