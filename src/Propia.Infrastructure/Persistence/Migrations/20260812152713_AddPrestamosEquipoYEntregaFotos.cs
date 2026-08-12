using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPrestamosEquipoYEntregaFotos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "devolucion_observacion",
                table: "reservas",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "devuelta_at",
                table: "reservas",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "devuelta_por_persona_id",
                table: "reservas",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "entrega_observacion",
                table: "reservas",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "entregada_at",
                table: "reservas",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "entregada_por_persona_id",
                table: "reservas",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "entrega_fotos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    origen_tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    origen_id = table.Column<Guid>(type: "uuid", nullable: false),
                    url = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    momento = table.Column<int>(type: "integer", nullable: false),
                    registrado_por_persona_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_entrega_fotos", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "prestamos_equipo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    equipo_activo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    persona_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidad_privada_id = table.Column<Guid>(type: "uuid", nullable: true),
                    fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    hora_inicio = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    hora_fin = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    estado = table.Column<int>(type: "integer", nullable: false),
                    observaciones = table.Column<string>(type: "text", nullable: true),
                    entregado_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    entregado_por_persona_id = table.Column<Guid>(type: "uuid", nullable: true),
                    entrega_observacion = table.Column<string>(type: "text", nullable: true),
                    devuelto_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    devuelto_por_persona_id = table.Column<Guid>(type: "uuid", nullable: true),
                    devolucion_observacion = table.Column<string>(type: "text", nullable: true),
                    motivo_cancelacion = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prestamos_equipo", x => x.id);
                    table.ForeignKey(
                        name: "FK_prestamos_equipo_equipos_activos_equipo_activo_id",
                        column: x => x.equipo_activo_id,
                        principalTable: "equipos_activos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_prestamos_equipo_personas_persona_id",
                        column: x => x.persona_id,
                        principalTable: "personas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_prestamos_equipo_unidades_privadas_unidad_privada_id",
                        column: x => x.unidad_privada_id,
                        principalTable: "unidades_privadas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_entrega_fotos_tenant_id",
                table: "entrega_fotos",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_entrega_fotos_tenant_id_origen_tipo_origen_id",
                table: "entrega_fotos",
                columns: new[] { "tenant_id", "origen_tipo", "origen_id" });

            migrationBuilder.CreateIndex(
                name: "IX_prestamos_equipo_equipo_activo_id",
                table: "prestamos_equipo",
                column: "equipo_activo_id");

            migrationBuilder.CreateIndex(
                name: "IX_prestamos_equipo_persona_id",
                table: "prestamos_equipo",
                column: "persona_id");

            migrationBuilder.CreateIndex(
                name: "IX_prestamos_equipo_tenant_id",
                table: "prestamos_equipo",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_prestamos_equipo_tenant_id_codigo",
                table: "prestamos_equipo",
                columns: new[] { "tenant_id", "codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_prestamos_equipo_tenant_id_equipo_activo_id_fecha",
                table: "prestamos_equipo",
                columns: new[] { "tenant_id", "equipo_activo_id", "fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_prestamos_equipo_unidad_privada_id",
                table: "prestamos_equipo",
                column: "unidad_privada_id");

            // RLS por tenant en las tablas nuevas (mismo patron que el resto).
            migrationBuilder.Sql(@"
                ALTER TABLE prestamos_equipo ENABLE ROW LEVEL SECURITY;
                ALTER TABLE prestamos_equipo FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON prestamos_equipo
                    USING (tenant_id = current_tenant_id()) WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON prestamos_equipo TO propia_app;

                ALTER TABLE entrega_fotos ENABLE ROW LEVEL SECURITY;
                ALTER TABLE entrega_fotos FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON entrega_fotos
                    USING (tenant_id = current_tenant_id()) WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON entrega_fotos TO propia_app;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP POLICY IF EXISTS tenant_isolation ON entrega_fotos;");
            migrationBuilder.Sql("DROP POLICY IF EXISTS tenant_isolation ON prestamos_equipo;");

            migrationBuilder.DropTable(
                name: "entrega_fotos");

            migrationBuilder.DropTable(
                name: "prestamos_equipo");

            migrationBuilder.DropColumn(
                name: "devolucion_observacion",
                table: "reservas");

            migrationBuilder.DropColumn(
                name: "devuelta_at",
                table: "reservas");

            migrationBuilder.DropColumn(
                name: "devuelta_por_persona_id",
                table: "reservas");

            migrationBuilder.DropColumn(
                name: "entrega_observacion",
                table: "reservas");

            migrationBuilder.DropColumn(
                name: "entregada_at",
                table: "reservas");

            migrationBuilder.DropColumn(
                name: "entregada_por_persona_id",
                table: "reservas");
        }
    }
}
