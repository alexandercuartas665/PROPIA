using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPqrsdModulo29 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pqrsd_categorias",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    es_predeterminada = table.Column<bool>(type: "boolean", nullable: false),
                    activa = table.Column<bool>(type: "boolean", nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pqrsd_categorias", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "pqrsd_configuracion_plazos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    dias_habiles = table.Column<int>(type: "integer", nullable: false),
                    dias_inconformidad = table.Column<int>(type: "integer", nullable: false),
                    nivel_urgencia = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pqrsd_configuracion_plazos", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "pqrsd_expedientes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero_radicado = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    categoria_id = table.Column<Guid>(type: "uuid", nullable: false),
                    descripcion = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    estado = table.Column<int>(type: "integer", nullable: false),
                    radicador_persona_id = table.Column<Guid>(type: "uuid", nullable: false),
                    identidad_reservada = table.Column<bool>(type: "boolean", nullable: false),
                    tutela_activa = table.Column<bool>(type: "boolean", nullable: false),
                    tutela_activada_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tutela_activada_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    fecha_vencimiento = table.Column<DateOnly>(type: "date", nullable: false),
                    tarea_id = table.Column<Guid>(type: "uuid", nullable: true),
                    respuesta_admin = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    respuesta_admin_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    respuesta_admin_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    inconformidad_texto = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    inconformidad_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    respuesta_definitiva = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    respuesta_definitiva_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    fecha_cierre = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cerrado_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pqrsd_expedientes", x => x.id);
                    table.ForeignKey(
                        name: "FK_pqrsd_expedientes_personas_radicador_persona_id",
                        column: x => x.radicador_persona_id,
                        principalTable: "personas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_pqrsd_expedientes_pqrsd_categorias_categoria_id",
                        column: x => x.categoria_id,
                        principalTable: "pqrsd_categorias",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pqrsd_adjuntos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    expediente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre_archivo = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    tipo_mime = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    tamanio_bytes = table.Column<long>(type: "bigint", nullable: false),
                    url_storage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    subido_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pqrsd_adjuntos", x => x.id);
                    table.ForeignKey(
                        name: "FK_pqrsd_adjuntos_pqrsd_expedientes_expediente_id",
                        column: x => x.expediente_id,
                        principalTable: "pqrsd_expedientes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pqrsd_comite_sesiones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    expediente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha_sesion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modalidad = table.Column<int>(type: "integer", nullable: false),
                    enlace_reunion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    resultado = table.Column<int>(type: "integer", nullable: true),
                    borrador_acta = table.Column<string>(type: "text", nullable: true),
                    acta_final = table.Column<string>(type: "text", nullable: true),
                    acta_documento_id = table.Column<Guid>(type: "uuid", nullable: true),
                    activada_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pqrsd_comite_sesiones", x => x.id);
                    table.ForeignKey(
                        name: "FK_pqrsd_comite_sesiones_pqrsd_expedientes_expediente_id",
                        column: x => x.expediente_id,
                        principalTable: "pqrsd_expedientes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pqrsd_historial_estados",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    expediente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    estado_anterior = table.Column<int>(type: "integer", nullable: true),
                    estado_nuevo = table.Column<int>(type: "integer", nullable: false),
                    actor_usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    origen = table.Column<int>(type: "integer", nullable: false),
                    nota = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pqrsd_historial_estados", x => x.id);
                    table.ForeignKey(
                        name: "FK_pqrsd_historial_estados_pqrsd_expedientes_expediente_id",
                        column: x => x.expediente_id,
                        principalTable: "pqrsd_expedientes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pqrsd_comite_miembros",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sesion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    persona_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pqrsd_comite_miembros", x => x.id);
                    table.ForeignKey(
                        name: "FK_pqrsd_comite_miembros_personas_persona_id",
                        column: x => x.persona_id,
                        principalTable: "personas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_pqrsd_comite_miembros_pqrsd_comite_sesiones_sesion_id",
                        column: x => x.sesion_id,
                        principalTable: "pqrsd_comite_sesiones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_pqrsd_adjuntos_expediente_id",
                table: "pqrsd_adjuntos",
                column: "expediente_id");

            migrationBuilder.CreateIndex(
                name: "IX_pqrsd_adjuntos_tenant_id",
                table: "pqrsd_adjuntos",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_pqrsd_categorias_tenant_id",
                table: "pqrsd_categorias",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_pqrsd_categorias_tenant_id_nombre",
                table: "pqrsd_categorias",
                columns: new[] { "tenant_id", "nombre" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pqrsd_comite_miembros_persona_id",
                table: "pqrsd_comite_miembros",
                column: "persona_id");

            migrationBuilder.CreateIndex(
                name: "IX_pqrsd_comite_miembros_sesion_id_persona_id",
                table: "pqrsd_comite_miembros",
                columns: new[] { "sesion_id", "persona_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pqrsd_comite_miembros_tenant_id",
                table: "pqrsd_comite_miembros",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_pqrsd_comite_sesiones_expediente_id",
                table: "pqrsd_comite_sesiones",
                column: "expediente_id");

            migrationBuilder.CreateIndex(
                name: "IX_pqrsd_comite_sesiones_tenant_id",
                table: "pqrsd_comite_sesiones",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_pqrsd_configuracion_plazos_tenant_id",
                table: "pqrsd_configuracion_plazos",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_pqrsd_configuracion_plazos_tenant_id_tipo",
                table: "pqrsd_configuracion_plazos",
                columns: new[] { "tenant_id", "tipo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pqrsd_expedientes_categoria_id",
                table: "pqrsd_expedientes",
                column: "categoria_id");

            migrationBuilder.CreateIndex(
                name: "IX_pqrsd_expedientes_radicador_persona_id",
                table: "pqrsd_expedientes",
                column: "radicador_persona_id");

            migrationBuilder.CreateIndex(
                name: "IX_pqrsd_expedientes_tenant_id",
                table: "pqrsd_expedientes",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_pqrsd_expedientes_tenant_id_estado",
                table: "pqrsd_expedientes",
                columns: new[] { "tenant_id", "estado" });

            migrationBuilder.CreateIndex(
                name: "IX_pqrsd_expedientes_tenant_id_numero_radicado",
                table: "pqrsd_expedientes",
                columns: new[] { "tenant_id", "numero_radicado" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pqrsd_expedientes_tenant_id_tipo",
                table: "pqrsd_expedientes",
                columns: new[] { "tenant_id", "tipo" });

            migrationBuilder.CreateIndex(
                name: "IX_pqrsd_historial_estados_expediente_id_created_at",
                table: "pqrsd_historial_estados",
                columns: new[] { "expediente_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_pqrsd_historial_estados_tenant_id",
                table: "pqrsd_historial_estados",
                column: "tenant_id");

            // RLS + GRANTs para las 7 tablas del modulo 2.9
            migrationBuilder.Sql(@"
                ALTER TABLE pqrsd_categorias ENABLE ROW LEVEL SECURITY;
                ALTER TABLE pqrsd_categorias FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON pqrsd_categorias
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON pqrsd_categorias TO propia_app;

                ALTER TABLE pqrsd_configuracion_plazos ENABLE ROW LEVEL SECURITY;
                ALTER TABLE pqrsd_configuracion_plazos FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON pqrsd_configuracion_plazos
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON pqrsd_configuracion_plazos TO propia_app;

                ALTER TABLE pqrsd_expedientes ENABLE ROW LEVEL SECURITY;
                ALTER TABLE pqrsd_expedientes FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON pqrsd_expedientes
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON pqrsd_expedientes TO propia_app;

                ALTER TABLE pqrsd_adjuntos ENABLE ROW LEVEL SECURITY;
                ALTER TABLE pqrsd_adjuntos FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON pqrsd_adjuntos
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON pqrsd_adjuntos TO propia_app;

                -- pqrsd_historial_estados: append-only (spec 2.9 v1.0 - sin UPDATE ni DELETE)
                ALTER TABLE pqrsd_historial_estados ENABLE ROW LEVEL SECURITY;
                ALTER TABLE pqrsd_historial_estados FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON pqrsd_historial_estados
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT ON pqrsd_historial_estados TO propia_app;

                ALTER TABLE pqrsd_comite_sesiones ENABLE ROW LEVEL SECURITY;
                ALTER TABLE pqrsd_comite_sesiones FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON pqrsd_comite_sesiones
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON pqrsd_comite_sesiones TO propia_app;

                ALTER TABLE pqrsd_comite_miembros ENABLE ROW LEVEL SECURITY;
                ALTER TABLE pqrsd_comite_miembros FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON pqrsd_comite_miembros
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON pqrsd_comite_miembros TO propia_app;
            ");

            // Trigger append-only en pqrsd_historial_estados (spec 2.9 v1.0 nota dev)
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION pqrsd_historial_append_only()
                RETURNS TRIGGER AS $$
                BEGIN
                    RAISE EXCEPTION 'pqrsd_historial_estados es append-only: % no permitido', TG_OP;
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER pqrsd_hist_no_update
                    BEFORE UPDATE ON pqrsd_historial_estados
                    FOR EACH ROW EXECUTE FUNCTION pqrsd_historial_append_only();

                CREATE TRIGGER pqrsd_hist_no_delete
                    BEFORE DELETE ON pqrsd_historial_estados
                    FOR EACH ROW EXECUTE FUNCTION pqrsd_historial_append_only();
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP TRIGGER IF EXISTS pqrsd_hist_no_delete ON pqrsd_historial_estados;
                DROP TRIGGER IF EXISTS pqrsd_hist_no_update ON pqrsd_historial_estados;
                DROP FUNCTION IF EXISTS pqrsd_historial_append_only();
            ");

            migrationBuilder.DropTable(
                name: "pqrsd_adjuntos");

            migrationBuilder.DropTable(
                name: "pqrsd_comite_miembros");

            migrationBuilder.DropTable(
                name: "pqrsd_configuracion_plazos");

            migrationBuilder.DropTable(
                name: "pqrsd_historial_estados");

            migrationBuilder.DropTable(
                name: "pqrsd_comite_sesiones");

            migrationBuilder.DropTable(
                name: "pqrsd_expedientes");

            migrationBuilder.DropTable(
                name: "pqrsd_categorias");
        }
    }
}
