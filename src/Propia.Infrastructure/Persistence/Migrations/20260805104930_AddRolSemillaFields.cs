using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRolSemillaFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "facetas_semilla",
                table: "roles_copropiedad",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "solo_directorio",
                table: "roles_copropiedad",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "facetas_semilla",
                table: "roles_copropiedad");

            migrationBuilder.DropColumn(
                name: "solo_directorio",
                table: "roles_copropiedad");
        }
    }
}
