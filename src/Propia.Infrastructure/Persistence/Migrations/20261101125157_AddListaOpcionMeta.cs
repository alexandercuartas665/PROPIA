using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddListaOpcionMeta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "meta",
                table: "lista_opciones",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "override_meta",
                table: "lista_opciones",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "meta",
                table: "lista_opciones");

            migrationBuilder.DropColumn(
                name: "override_meta",
                table: "lista_opciones");
        }
    }
}
