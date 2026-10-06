using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogoOpcion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "catalogo_opciones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    lista = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    clave = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    color = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    es_semilla = table.Column<bool>(type: "boolean", nullable: false),
                    meta = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_catalogo_opciones", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_catalogo_opciones_lista_clave",
                table: "catalogo_opciones",
                columns: new[] { "lista", "clave" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_catalogo_opciones_lista_orden",
                table: "catalogo_opciones",
                columns: new[] { "lista", "orden" });

            // Tabla GLOBAL de plataforma (sin tenant_id): NO lleva RLS. El aislamiento no aplica (es
            // un catalogo compartido de lectura); la escritura se gatea a nivel de app (consola A&D).
            // El rol de aplicacion necesita CRUD para leer el catalogo y (desde A&D) editarlo.
            migrationBuilder.Sql("GRANT SELECT, INSERT, UPDATE, DELETE ON catalogo_opciones TO propia_app;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "catalogo_opciones");
        }
    }
}
