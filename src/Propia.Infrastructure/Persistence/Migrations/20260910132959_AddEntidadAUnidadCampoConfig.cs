using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEntidadAUnidadCampoConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_unidad_campos_config_tenant_id_campo_clave",
                table: "unidad_campos_config");

            migrationBuilder.AddColumn<string>(
                name: "entidad",
                table: "unidad_campos_config",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "unidad");

            migrationBuilder.CreateIndex(
                name: "IX_unidad_campos_config_tenant_id_entidad_campo_clave",
                table: "unidad_campos_config",
                columns: new[] { "tenant_id", "entidad", "campo_clave" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_unidad_campos_config_tenant_id_entidad_campo_clave",
                table: "unidad_campos_config");

            migrationBuilder.DropColumn(
                name: "entidad",
                table: "unidad_campos_config");

            migrationBuilder.CreateIndex(
                name: "IX_unidad_campos_config_tenant_id_campo_clave",
                table: "unidad_campos_config",
                columns: new[] { "tenant_id", "campo_clave" },
                unique: true);
        }
    }
}
