using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPqrsdTableroConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "archivado",
                table: "pqrsd_expedientes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "archivado_at",
                table: "pqrsd_expedientes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "archivado_por_usuario_id",
                table: "pqrsd_expedientes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "estado_id",
                table: "pqrsd_expedientes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "unidad_privada_id",
                table: "pqrsd_expedientes",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "pqrsd_campo_valores",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    expediente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pqrsd_campo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    valor = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pqrsd_campo_valores", x => x.id);
                    table.ForeignKey(
                        name: "FK_pqrsd_campo_valores_pqrsd_expedientes_expediente_id",
                        column: x => x.expediente_id,
                        principalTable: "pqrsd_expedientes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pqrsd_campos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    label = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    opciones = table.Column<string>(type: "text", nullable: true),
                    mostrar_en_filtro = table.Column<bool>(type: "boolean", nullable: false),
                    columna = table.Column<int>(type: "integer", nullable: false),
                    descripcion = table.Column<string>(type: "text", nullable: true),
                    requerido = table.Column<bool>(type: "boolean", nullable: false),
                    valor_por_defecto = table.Column<string>(type: "text", nullable: true),
                    permite_varios = table.Column<bool>(type: "boolean", nullable: false),
                    campos_suma = table.Column<string>(type: "text", nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pqrsd_campos", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "pqrsd_estados",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    es_terminal = table.Column<bool>(type: "boolean", nullable: false),
                    es_base = table.Column<bool>(type: "boolean", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    semantica_legal = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pqrsd_estados", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_pqrsd_expedientes_estado_id",
                table: "pqrsd_expedientes",
                column: "estado_id");

            migrationBuilder.CreateIndex(
                name: "IX_pqrsd_expedientes_tenant_id_archivado",
                table: "pqrsd_expedientes",
                columns: new[] { "tenant_id", "archivado" });

            migrationBuilder.CreateIndex(
                name: "IX_pqrsd_expedientes_tenant_id_estado_id",
                table: "pqrsd_expedientes",
                columns: new[] { "tenant_id", "estado_id" });

            migrationBuilder.CreateIndex(
                name: "IX_pqrsd_campo_valores_expediente_id_pqrsd_campo_id",
                table: "pqrsd_campo_valores",
                columns: new[] { "expediente_id", "pqrsd_campo_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pqrsd_campo_valores_tenant_id",
                table: "pqrsd_campo_valores",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_pqrsd_campos_tenant_id",
                table: "pqrsd_campos",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_pqrsd_estados_tenant_id",
                table: "pqrsd_estados",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_pqrsd_estados_tenant_id_nombre",
                table: "pqrsd_estados",
                columns: new[] { "tenant_id", "nombre" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_pqrsd_expedientes_pqrsd_estados_estado_id",
                table: "pqrsd_expedientes",
                column: "estado_id",
                principalTable: "pqrsd_estados",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            // RLS por tenant en las 3 tablas nuevas (mismo patron que el resto del modelo).
            migrationBuilder.Sql(@"
                ALTER TABLE pqrsd_estados ENABLE ROW LEVEL SECURITY;
                ALTER TABLE pqrsd_estados FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON pqrsd_estados
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON pqrsd_estados TO propia_app;

                ALTER TABLE pqrsd_campos ENABLE ROW LEVEL SECURITY;
                ALTER TABLE pqrsd_campos FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON pqrsd_campos
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON pqrsd_campos TO propia_app;

                ALTER TABLE pqrsd_campo_valores ENABLE ROW LEVEL SECURITY;
                ALTER TABLE pqrsd_campo_valores FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON pqrsd_campo_valores
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON pqrsd_campo_valores TO propia_app;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_pqrsd_expedientes_pqrsd_estados_estado_id",
                table: "pqrsd_expedientes");

            migrationBuilder.DropTable(
                name: "pqrsd_campo_valores");

            migrationBuilder.DropTable(
                name: "pqrsd_campos");

            migrationBuilder.DropTable(
                name: "pqrsd_estados");

            migrationBuilder.DropIndex(
                name: "IX_pqrsd_expedientes_estado_id",
                table: "pqrsd_expedientes");

            migrationBuilder.DropIndex(
                name: "IX_pqrsd_expedientes_tenant_id_archivado",
                table: "pqrsd_expedientes");

            migrationBuilder.DropIndex(
                name: "IX_pqrsd_expedientes_tenant_id_estado_id",
                table: "pqrsd_expedientes");

            migrationBuilder.DropColumn(
                name: "archivado",
                table: "pqrsd_expedientes");

            migrationBuilder.DropColumn(
                name: "archivado_at",
                table: "pqrsd_expedientes");

            migrationBuilder.DropColumn(
                name: "archivado_por_usuario_id",
                table: "pqrsd_expedientes");

            migrationBuilder.DropColumn(
                name: "estado_id",
                table: "pqrsd_expedientes");

            migrationBuilder.DropColumn(
                name: "unidad_privada_id",
                table: "pqrsd_expedientes");
        }
    }
}
