using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class S02UniquePersonaIdEnUsuarios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_asp_net_users_persona_id",
                table: "asp_net_users");

            migrationBuilder.CreateIndex(
                name: "IX_asp_net_users_persona_id",
                table: "asp_net_users",
                column: "persona_id",
                unique: true,
                filter: "persona_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_asp_net_users_persona_id",
                table: "asp_net_users");

            migrationBuilder.CreateIndex(
                name: "IX_asp_net_users_persona_id",
                table: "asp_net_users",
                column: "persona_id");
        }
    }
}
