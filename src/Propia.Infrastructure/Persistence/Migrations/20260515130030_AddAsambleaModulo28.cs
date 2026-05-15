using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAsambleaModulo28 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "asamblea_config",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    plazo_citacion_dias = table.Column<int>(type: "integer", nullable: false),
                    limite_poderes_por_persona = table.Column<int>(type: "integer", nullable: true),
                    gracia_reconexion_seg = table.Column<int>(type: "integer", nullable: false),
                    notif_recordatorio_dias = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_asamblea_config", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "elecciones_consejo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sesion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    punto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    periodo_inicio = table.Column<DateOnly>(type: "date", nullable: false),
                    periodo_fin = table.Column<DateOnly>(type: "date", nullable: true),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_elecciones_consejo", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sesion_quorum_log",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sesion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    evento = table.Column<int>(type: "integer", nullable: false),
                    persona_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidad_privada_id = table.Column<Guid>(type: "uuid", nullable: false),
                    coeficiente = table.Column<decimal>(type: "numeric(10,6)", precision: 10, scale: 6, nullable: false),
                    quorum_acumulado_pct = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sesion_quorum_log", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sesiones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    modalidad = table.Column<int>(type: "integer", nullable: false),
                    estado = table.Column<int>(type: "integer", nullable: false),
                    titulo = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    fecha_sesion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    lugar_fisico = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    enlace_video = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    plazo_citacion_dias = table.Column<int>(type: "integer", nullable: false),
                    fecha_citacion_enviada = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    segunda_convocatoria = table.Column<bool>(type: "boolean", nullable: false),
                    sesion_padre_id = table.Column<Guid>(type: "uuid", nullable: true),
                    quorum_requerido_pct = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    hora_apertura = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    hora_cierre = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    quorum_alcanzado = table.Column<bool>(type: "boolean", nullable: true),
                    creado_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sesiones", x => x.id);
                    table.ForeignKey(
                        name: "FK_sesiones_sesiones_sesion_padre_id",
                        column: x => x.sesion_padre_id,
                        principalTable: "sesiones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "eleccion_candidatos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    eleccion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    persona_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cargo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    votos_coeficiente = table.Column<decimal>(type: "numeric(10,6)", precision: 10, scale: 6, nullable: false),
                    elegido = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eleccion_candidatos", x => x.id);
                    table.ForeignKey(
                        name: "FK_eleccion_candidatos_elecciones_consejo_eleccion_id",
                        column: x => x.eleccion_id,
                        principalTable: "elecciones_consejo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_eleccion_candidatos_personas_persona_id",
                        column: x => x.persona_id,
                        principalTable: "personas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "actas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sesion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    estado = table.Column<int>(type: "integer", nullable: false),
                    contenido_generado = table.Column<string>(type: "text", nullable: false),
                    narrativa_secretario = table.Column<string>(type: "text", nullable: true),
                    documento_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    hash_documento = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    firmado_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tipo_firma = table.Column<int>(type: "integer", nullable: true),
                    timestamp_firma = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    firmante_ip = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    publicada_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_actas", x => x.id);
                    table.ForeignKey(
                        name: "FK_actas_sesiones_sesion_id",
                        column: x => x.sesion_id,
                        principalTable: "sesiones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sesion_participantes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sesion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    persona_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidad_privada_id = table.Column<Guid>(type: "uuid", nullable: false),
                    coeficiente = table.Column<decimal>(type: "numeric(10,6)", precision: 10, scale: 6, nullable: false),
                    calidad = table.Column<int>(type: "integer", nullable: false),
                    presente = table.Column<bool>(type: "boolean", nullable: false),
                    hora_ingreso = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    hora_salida = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sesion_participantes", x => x.id);
                    table.ForeignKey(
                        name: "FK_sesion_participantes_personas_persona_id",
                        column: x => x.persona_id,
                        principalTable: "personas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sesion_participantes_sesiones_sesion_id",
                        column: x => x.sesion_id,
                        principalTable: "sesiones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_sesion_participantes_unidades_privadas_unidad_privada_id",
                        column: x => x.unidad_privada_id,
                        principalTable: "unidades_privadas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sesion_poderes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sesion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    otorgante_usuario_persona_id = table.Column<Guid>(type: "uuid", nullable: false),
                    otorgante_unidad_id = table.Column<Guid>(type: "uuid", nullable: false),
                    apoderado_persona_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_poder = table.Column<int>(type: "integer", nullable: false),
                    estado = table.Column<int>(type: "integer", nullable: false),
                    documento_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    hash_poder = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    timestamp_firma = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    firmante_ip = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    aprobado_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    nota_rechazo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sesion_poderes", x => x.id);
                    table.ForeignKey(
                        name: "FK_sesion_poderes_personas_apoderado_persona_id",
                        column: x => x.apoderado_persona_id,
                        principalTable: "personas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sesion_poderes_sesiones_sesion_id",
                        column: x => x.sesion_id,
                        principalTable: "sesiones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sesion_puntos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sesion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero = table.Column<int>(type: "integer", nullable: false),
                    titulo = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    requiere_votacion = table.Column<bool>(type: "boolean", nullable: false),
                    tipo_mayoria = table.Column<int>(type: "integer", nullable: false),
                    mayoria_pct = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    modalidad_voto = table.Column<int>(type: "integer", nullable: false),
                    opciones_voto = table.Column<string>(type: "text", nullable: false),
                    presupuesto_id = table.Column<Guid>(type: "uuid", nullable: true),
                    narrativa_secretario = table.Column<string>(type: "text", nullable: true),
                    estado = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sesion_puntos", x => x.id);
                    table.ForeignKey(
                        name: "FK_sesion_puntos_sesiones_sesion_id",
                        column: x => x.sesion_id,
                        principalTable: "sesiones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sesion_documentos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sesion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    punto_id = table.Column<Guid>(type: "uuid", nullable: true),
                    nombre = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    url_storage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    tipo_archivo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    tamanio_bytes = table.Column<long>(type: "bigint", nullable: false),
                    visibilidad = table.Column<int>(type: "integer", nullable: false),
                    subido_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sesion_documentos", x => x.id);
                    table.ForeignKey(
                        name: "FK_sesion_documentos_sesion_puntos_punto_id",
                        column: x => x.punto_id,
                        principalTable: "sesion_puntos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_sesion_documentos_sesiones_sesion_id",
                        column: x => x.sesion_id,
                        principalTable: "sesiones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "votaciones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sesion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    punto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    estado = table.Column<int>(type: "integer", nullable: false),
                    hora_apertura = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    hora_cierre = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    quorum_al_abrir_pct = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    coeficiente_total_sala = table.Column<decimal>(type: "numeric(10,6)", precision: 10, scale: 6, nullable: false),
                    resultado_opcion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    resultado_pct = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    resultado_final = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_votaciones", x => x.id);
                    table.ForeignKey(
                        name: "FK_votaciones_sesion_puntos_punto_id",
                        column: x => x.punto_id,
                        principalTable: "sesion_puntos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_votaciones_sesiones_sesion_id",
                        column: x => x.sesion_id,
                        principalTable: "sesiones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "votos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    votacion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    persona_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidad_privada_id = table.Column<Guid>(type: "uuid", nullable: false),
                    coeficiente_aportado = table.Column<decimal>(type: "numeric(10,6)", precision: 10, scale: 6, nullable: false),
                    opcion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    es_secreto = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_votos", x => x.id);
                    table.ForeignKey(
                        name: "FK_votos_votaciones_votacion_id",
                        column: x => x.votacion_id,
                        principalTable: "votaciones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_actas_sesion_id",
                table: "actas",
                column: "sesion_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_actas_tenant_id",
                table: "actas",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_asamblea_config_tenant_id",
                table: "asamblea_config",
                column: "tenant_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_eleccion_candidatos_eleccion_id_persona_id",
                table: "eleccion_candidatos",
                columns: new[] { "eleccion_id", "persona_id" });

            migrationBuilder.CreateIndex(
                name: "IX_eleccion_candidatos_persona_id",
                table: "eleccion_candidatos",
                column: "persona_id");

            migrationBuilder.CreateIndex(
                name: "IX_eleccion_candidatos_tenant_id",
                table: "eleccion_candidatos",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_elecciones_consejo_sesion_id",
                table: "elecciones_consejo",
                column: "sesion_id");

            migrationBuilder.CreateIndex(
                name: "IX_elecciones_consejo_tenant_id",
                table: "elecciones_consejo",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_sesion_documentos_punto_id",
                table: "sesion_documentos",
                column: "punto_id");

            migrationBuilder.CreateIndex(
                name: "IX_sesion_documentos_sesion_id",
                table: "sesion_documentos",
                column: "sesion_id");

            migrationBuilder.CreateIndex(
                name: "IX_sesion_documentos_tenant_id",
                table: "sesion_documentos",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_sesion_participantes_persona_id",
                table: "sesion_participantes",
                column: "persona_id");

            migrationBuilder.CreateIndex(
                name: "IX_sesion_participantes_sesion_id_unidad_privada_id",
                table: "sesion_participantes",
                columns: new[] { "sesion_id", "unidad_privada_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sesion_participantes_tenant_id",
                table: "sesion_participantes",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_sesion_participantes_unidad_privada_id",
                table: "sesion_participantes",
                column: "unidad_privada_id");

            migrationBuilder.CreateIndex(
                name: "IX_sesion_poderes_apoderado_persona_id",
                table: "sesion_poderes",
                column: "apoderado_persona_id");

            migrationBuilder.CreateIndex(
                name: "IX_sesion_poderes_sesion_id_otorgante_unidad_id",
                table: "sesion_poderes",
                columns: new[] { "sesion_id", "otorgante_unidad_id" });

            migrationBuilder.CreateIndex(
                name: "IX_sesion_poderes_tenant_id",
                table: "sesion_poderes",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_sesion_puntos_sesion_id",
                table: "sesion_puntos",
                column: "sesion_id");

            migrationBuilder.CreateIndex(
                name: "IX_sesion_puntos_tenant_id",
                table: "sesion_puntos",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_sesion_quorum_log_sesion_id_created_at",
                table: "sesion_quorum_log",
                columns: new[] { "sesion_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_sesion_quorum_log_tenant_id",
                table: "sesion_quorum_log",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_sesiones_sesion_padre_id",
                table: "sesiones",
                column: "sesion_padre_id");

            migrationBuilder.CreateIndex(
                name: "IX_sesiones_tenant_id",
                table: "sesiones",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_sesiones_tenant_id_estado",
                table: "sesiones",
                columns: new[] { "tenant_id", "estado" });

            migrationBuilder.CreateIndex(
                name: "IX_sesiones_tenant_id_fecha_sesion",
                table: "sesiones",
                columns: new[] { "tenant_id", "fecha_sesion" });

            migrationBuilder.CreateIndex(
                name: "IX_votaciones_punto_id",
                table: "votaciones",
                column: "punto_id");

            migrationBuilder.CreateIndex(
                name: "IX_votaciones_sesion_id",
                table: "votaciones",
                column: "sesion_id");

            migrationBuilder.CreateIndex(
                name: "IX_votaciones_tenant_id",
                table: "votaciones",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_votos_tenant_id",
                table: "votos",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_votos_votacion_id_unidad_privada_id",
                table: "votos",
                columns: new[] { "votacion_id", "unidad_privada_id" },
                unique: true);

            // RLS + GRANTs para las 12 tablas del modulo 2.8
            migrationBuilder.Sql(@"
                ALTER TABLE asamblea_config ENABLE ROW LEVEL SECURITY;
                ALTER TABLE asamblea_config FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON asamblea_config
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON asamblea_config TO propia_app;

                ALTER TABLE sesiones ENABLE ROW LEVEL SECURITY;
                ALTER TABLE sesiones FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON sesiones
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON sesiones TO propia_app;

                ALTER TABLE sesion_puntos ENABLE ROW LEVEL SECURITY;
                ALTER TABLE sesion_puntos FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON sesion_puntos
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON sesion_puntos TO propia_app;

                ALTER TABLE sesion_documentos ENABLE ROW LEVEL SECURITY;
                ALTER TABLE sesion_documentos FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON sesion_documentos
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON sesion_documentos TO propia_app;

                ALTER TABLE sesion_participantes ENABLE ROW LEVEL SECURITY;
                ALTER TABLE sesion_participantes FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON sesion_participantes
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON sesion_participantes TO propia_app;

                ALTER TABLE sesion_poderes ENABLE ROW LEVEL SECURITY;
                ALTER TABLE sesion_poderes FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON sesion_poderes
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON sesion_poderes TO propia_app;

                -- sesion_quorum_log: append-only por Decreto 1074/2015
                ALTER TABLE sesion_quorum_log ENABLE ROW LEVEL SECURITY;
                ALTER TABLE sesion_quorum_log FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON sesion_quorum_log
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT ON sesion_quorum_log TO propia_app;

                ALTER TABLE votaciones ENABLE ROW LEVEL SECURITY;
                ALTER TABLE votaciones FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON votaciones
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON votaciones TO propia_app;

                ALTER TABLE votos ENABLE ROW LEVEL SECURITY;
                ALTER TABLE votos FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON votos
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON votos TO propia_app;

                -- actas: solo INSERT/UPDATE permitido (RN-10 inmutable tras firma se controla en aplicacion)
                ALTER TABLE actas ENABLE ROW LEVEL SECURITY;
                ALTER TABLE actas FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON actas
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE ON actas TO propia_app;

                ALTER TABLE elecciones_consejo ENABLE ROW LEVEL SECURITY;
                ALTER TABLE elecciones_consejo FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON elecciones_consejo
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON elecciones_consejo TO propia_app;

                ALTER TABLE eleccion_candidatos ENABLE ROW LEVEL SECURITY;
                ALTER TABLE eleccion_candidatos FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON eleccion_candidatos
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON eleccion_candidatos TO propia_app;
            ");

            // Trigger append-only en sesion_quorum_log (Decreto 1074/2015 - evidencia legal de quorum continuo)
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION sesion_quorum_log_append_only()
                RETURNS TRIGGER AS $$
                BEGIN
                    RAISE EXCEPTION 'sesion_quorum_log es append-only por Decreto 1074/2015: % no permitido', TG_OP;
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER sesion_quorum_no_update
                    BEFORE UPDATE ON sesion_quorum_log
                    FOR EACH ROW EXECUTE FUNCTION sesion_quorum_log_append_only();

                CREATE TRIGGER sesion_quorum_no_delete
                    BEFORE DELETE ON sesion_quorum_log
                    FOR EACH ROW EXECUTE FUNCTION sesion_quorum_log_append_only();
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP TRIGGER IF EXISTS sesion_quorum_no_delete ON sesion_quorum_log;
                DROP TRIGGER IF EXISTS sesion_quorum_no_update ON sesion_quorum_log;
                DROP FUNCTION IF EXISTS sesion_quorum_log_append_only();
            ");

            migrationBuilder.DropTable(
                name: "actas");

            migrationBuilder.DropTable(
                name: "asamblea_config");

            migrationBuilder.DropTable(
                name: "eleccion_candidatos");

            migrationBuilder.DropTable(
                name: "sesion_documentos");

            migrationBuilder.DropTable(
                name: "sesion_participantes");

            migrationBuilder.DropTable(
                name: "sesion_poderes");

            migrationBuilder.DropTable(
                name: "sesion_quorum_log");

            migrationBuilder.DropTable(
                name: "votos");

            migrationBuilder.DropTable(
                name: "elecciones_consejo");

            migrationBuilder.DropTable(
                name: "votaciones");

            migrationBuilder.DropTable(
                name: "sesion_puntos");

            migrationBuilder.DropTable(
                name: "sesiones");
        }
    }
}
