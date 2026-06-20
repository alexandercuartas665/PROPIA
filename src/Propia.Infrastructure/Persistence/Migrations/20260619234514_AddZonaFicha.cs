using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddZonaFicha : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "imagen_url",
                table: "zonas_comunes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "mantenimiento_contrato",
                table: "zonas_comunes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "mantenimiento_dia_mes",
                table: "zonas_comunes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "mantenimiento_frecuencia",
                table: "zonas_comunes",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "mantenimiento_tipo",
                table: "zonas_comunes",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "zona_campos_personalizados",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    zona_comun_id = table.Column<Guid>(type: "uuid", nullable: false),
                    label = table.Column<string>(type: "text", nullable: false),
                    valor = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_zona_campos_personalizados", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "zona_documentos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    zona_comun_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "text", nullable: false),
                    url = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_zona_documentos", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "zona_facturas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    zona_comun_id = table.Column<Guid>(type: "uuid", nullable: false),
                    concepto = table.Column<string>(type: "text", nullable: false),
                    valor = table.Column<decimal>(type: "numeric", nullable: true),
                    fecha = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_zona_facturas", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "zona_novedad_comentarios",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    zona_novedad_id = table.Column<Guid>(type: "uuid", nullable: false),
                    autor_nombre = table.Column<string>(type: "text", nullable: false),
                    autor_persona_id = table.Column<Guid>(type: "uuid", nullable: true),
                    texto = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_zona_novedad_comentarios", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "zona_novedad_likes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    zona_novedad_id = table.Column<Guid>(type: "uuid", nullable: false),
                    persona_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_zona_novedad_likes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "zona_novedades",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    zona_comun_id = table.Column<Guid>(type: "uuid", nullable: false),
                    titulo = table.Column<string>(type: "text", nullable: false),
                    texto = table.Column<string>(type: "text", nullable: true),
                    imagen_url = table.Column<string>(type: "text", nullable: true),
                    autor_nombre = table.Column<string>(type: "text", nullable: false),
                    autor_persona_id = table.Column<Guid>(type: "uuid", nullable: true),
                    likes_count = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_zona_novedades", x => x.id);
                });

            // RLS por tenant para las tablas nuevas (mismo patron que el resto del modelo).
            foreach (var tabla in new[] { "zona_facturas", "zona_documentos", "zona_campos_personalizados", "zona_novedades", "zona_novedad_comentarios", "zona_novedad_likes" })
            {
                migrationBuilder.Sql($@"
                    ALTER TABLE {tabla} ENABLE ROW LEVEL SECURITY;
                    ALTER TABLE {tabla} FORCE ROW LEVEL SECURITY;
                    CREATE POLICY tenant_isolation ON {tabla}
                        USING (tenant_id = current_tenant_id())
                        WITH CHECK (tenant_id = current_tenant_id());
                    GRANT SELECT, INSERT, UPDATE, DELETE ON {tabla} TO propia_app;
                ");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "zona_campos_personalizados");

            migrationBuilder.DropTable(
                name: "zona_documentos");

            migrationBuilder.DropTable(
                name: "zona_facturas");

            migrationBuilder.DropTable(
                name: "zona_novedad_comentarios");

            migrationBuilder.DropTable(
                name: "zona_novedad_likes");

            migrationBuilder.DropTable(
                name: "zona_novedades");

            migrationBuilder.DropColumn(
                name: "imagen_url",
                table: "zonas_comunes");

            migrationBuilder.DropColumn(
                name: "mantenimiento_contrato",
                table: "zonas_comunes");

            migrationBuilder.DropColumn(
                name: "mantenimiento_dia_mes",
                table: "zonas_comunes");

            migrationBuilder.DropColumn(
                name: "mantenimiento_frecuencia",
                table: "zonas_comunes");

            migrationBuilder.DropColumn(
                name: "mantenimiento_tipo",
                table: "zonas_comunes");
        }
    }
}
