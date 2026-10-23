using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TallerControl.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Las suscripciones push son endpoints de navegadores, no datos de negocio: al dar de baja un dispositivo (o cuando el
    /// servicio push responde que ya no existe) hay que poder borrarlas. Sin este permiso el envío fallaba entero.
    /// </summary>
    public partial class PushSubscriptionsDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("GRANT DELETE ON push_subscriptions TO tallercontrol_app;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("REVOKE DELETE ON push_subscriptions FROM tallercontrol_app;");
        }
    }
}
