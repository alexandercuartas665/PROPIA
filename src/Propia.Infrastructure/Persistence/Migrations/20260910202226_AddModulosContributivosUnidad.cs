using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddModulosContributivosUnidad : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "modulo_contributivo_1",
                table: "unidades_privadas",
                type: "numeric(7,4)",
                precision: 7,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "modulo_contributivo_2",
                table: "unidades_privadas",
                type: "numeric(7,4)",
                precision: 7,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "modulo_contributivo_3",
                table: "unidades_privadas",
                type: "numeric(7,4)",
                precision: 7,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "modulo_contributivo_4",
                table: "unidades_privadas",
                type: "numeric(7,4)",
                precision: 7,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "modulo_contributivo_5",
                table: "unidades_privadas",
                type: "numeric(7,4)",
                precision: 7,
                scale: 4,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "modulo_contributivo_1",
                table: "unidades_privadas");

            migrationBuilder.DropColumn(
                name: "modulo_contributivo_2",
                table: "unidades_privadas");

            migrationBuilder.DropColumn(
                name: "modulo_contributivo_3",
                table: "unidades_privadas");

            migrationBuilder.DropColumn(
                name: "modulo_contributivo_4",
                table: "unidades_privadas");

            migrationBuilder.DropColumn(
                name: "modulo_contributivo_5",
                table: "unidades_privadas");
        }
    }
}
