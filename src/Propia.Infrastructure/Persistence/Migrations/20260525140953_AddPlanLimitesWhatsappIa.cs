using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlanLimitesWhatsappIa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "limite_lineas_whatsapp",
                table: "planes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "limite_llamadas_ia_mensual",
                table: "planes",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "limite_lineas_whatsapp",
                table: "planes");

            migrationBuilder.DropColumn(
                name: "limite_llamadas_ia_mensual",
                table: "planes");
        }
    }
}
