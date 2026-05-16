using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReportesModulo216 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "reporte_categorias",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    icono = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    modulo_origen = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    es_activa = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reporte_categorias", x => x.id);
                    table.ForeignKey(
                        name: "FK_reporte_categorias_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "reporte_semaforo_config",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    indicador_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    umbral_amarillo = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    umbral_rojo = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    es_ascendente = table.Column<bool>(type: "boolean", nullable: false),
                    actualizado_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reporte_semaforo_config", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "reporte_catalogo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    categoria_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    modulo_origen = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    clave = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    audiencias_json = table.Column<string>(type: "text", nullable: false),
                    filtros_config_json = table.Column<string>(type: "text", nullable: true),
                    es_activo = table.Column<bool>(type: "boolean", nullable: false),
                    es_sistema = table.Column<bool>(type: "boolean", nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reporte_catalogo", x => x.id);
                    table.ForeignKey(
                        name: "FK_reporte_catalogo_reporte_categorias_categoria_id",
                        column: x => x.categoria_id,
                        principalTable: "reporte_categorias",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_reporte_catalogo_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "reporte_generados",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reporte_catalogo_id = table.Column<Guid>(type: "uuid", nullable: true),
                    nombre_reporte = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    categoria = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    periodo_inicio = table.Column<DateOnly>(type: "date", nullable: false),
                    periodo_fin = table.Column<DateOnly>(type: "date", nullable: false),
                    filtros_aplicados_json = table.Column<string>(type: "text", nullable: true),
                    origen = table.Column<int>(type: "integer", nullable: false),
                    prompt_ia = table.Column<string>(type: "text", nullable: true),
                    compartido_consejo = table.Column<bool>(type: "boolean", nullable: false),
                    compartido_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    compartido_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    estado = table.Column<int>(type: "integer", nullable: false),
                    error_mensaje = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    resultado_json = table.Column<string>(type: "text", nullable: true),
                    url_pdf = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    url_excel = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    url_expiracion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    generado_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reporte_generados", x => x.id);
                    table.ForeignKey(
                        name: "FK_reporte_generados_reporte_catalogo_reporte_catalogo_id",
                        column: x => x.reporte_catalogo_id,
                        principalTable: "reporte_catalogo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "reporte_programaciones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reporte_catalogo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    frecuencia = table.Column<int>(type: "integer", nullable: false),
                    dia_envio = table.Column<int>(type: "integer", nullable: false),
                    periodo_que_cubre = table.Column<int>(type: "integer", nullable: false),
                    filtros_aplicados_json = table.Column<string>(type: "text", nullable: true),
                    formato = table.Column<int>(type: "integer", nullable: false),
                    canales_json = table.Column<string>(type: "text", nullable: false),
                    estado = table.Column<int>(type: "integer", nullable: false),
                    proximo_envio = table.Column<DateOnly>(type: "date", nullable: true),
                    ultimo_envio = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ultimo_envio_exitoso = table.Column<bool>(type: "boolean", nullable: true),
                    creado_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reporte_programaciones", x => x.id);
                    table.ForeignKey(
                        name: "FK_reporte_programaciones_reporte_catalogo_reporte_catalogo_id",
                        column: x => x.reporte_catalogo_id,
                        principalTable: "reporte_catalogo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "reporte_programacion_destinatarios",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    programacion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    persona_id = table.Column<Guid>(type: "uuid", nullable: true),
                    email_externo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    whatsapp_externo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reporte_programacion_destinatarios", x => x.id);
                    table.ForeignKey(
                        name: "FK_reporte_programacion_destinatarios_personas_persona_id",
                        column: x => x.persona_id,
                        principalTable: "personas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_reporte_programacion_destinatarios_reporte_programaciones_p~",
                        column: x => x.programacion_id,
                        principalTable: "reporte_programaciones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_reporte_catalogo_categoria_id",
                table: "reporte_catalogo",
                column: "categoria_id");

            migrationBuilder.CreateIndex(
                name: "IX_reporte_catalogo_tenant_id",
                table: "reporte_catalogo",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_reporte_catalogo_tenant_id_clave",
                table: "reporte_catalogo",
                columns: new[] { "tenant_id", "clave" });

            migrationBuilder.CreateIndex(
                name: "IX_reporte_categorias_tenant_id",
                table: "reporte_categorias",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_reporte_categorias_tenant_id_nombre",
                table: "reporte_categorias",
                columns: new[] { "tenant_id", "nombre" });

            migrationBuilder.CreateIndex(
                name: "IX_reporte_generados_reporte_catalogo_id",
                table: "reporte_generados",
                column: "reporte_catalogo_id");

            migrationBuilder.CreateIndex(
                name: "IX_reporte_generados_tenant_id",
                table: "reporte_generados",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_reporte_generados_tenant_id_compartido_consejo",
                table: "reporte_generados",
                columns: new[] { "tenant_id", "compartido_consejo" });

            migrationBuilder.CreateIndex(
                name: "IX_reporte_generados_tenant_id_estado",
                table: "reporte_generados",
                columns: new[] { "tenant_id", "estado" });

            migrationBuilder.CreateIndex(
                name: "IX_reporte_generados_tenant_id_periodo_inicio_periodo_fin",
                table: "reporte_generados",
                columns: new[] { "tenant_id", "periodo_inicio", "periodo_fin" });

            migrationBuilder.CreateIndex(
                name: "IX_reporte_programacion_destinatarios_persona_id",
                table: "reporte_programacion_destinatarios",
                column: "persona_id");

            migrationBuilder.CreateIndex(
                name: "IX_reporte_programacion_destinatarios_programacion_id",
                table: "reporte_programacion_destinatarios",
                column: "programacion_id");

            migrationBuilder.CreateIndex(
                name: "IX_reporte_programacion_destinatarios_tenant_id",
                table: "reporte_programacion_destinatarios",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_reporte_programaciones_proximo_envio",
                table: "reporte_programaciones",
                column: "proximo_envio");

            migrationBuilder.CreateIndex(
                name: "IX_reporte_programaciones_reporte_catalogo_id",
                table: "reporte_programaciones",
                column: "reporte_catalogo_id");

            migrationBuilder.CreateIndex(
                name: "IX_reporte_programaciones_tenant_id",
                table: "reporte_programaciones",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_reporte_programaciones_tenant_id_estado",
                table: "reporte_programaciones",
                columns: new[] { "tenant_id", "estado" });

            migrationBuilder.CreateIndex(
                name: "IX_reporte_semaforo_config_tenant_id_indicador_key",
                table: "reporte_semaforo_config",
                columns: new[] { "tenant_id", "indicador_key" },
                unique: true);

            // -----------------------------------------------------------------
            // RLS + GRANTs (Spec 2.16)
            // -----------------------------------------------------------------

            // Tablas con tenant_id NOT NULL (operativas del tenant).
            foreach (var tabla in new[] {
                "reporte_generados",
                "reporte_programaciones",
                "reporte_programacion_destinatarios",
                "reporte_semaforo_config"
            })
            {
                migrationBuilder.Sql($@"
                    ALTER TABLE {tabla} ENABLE ROW LEVEL SECURITY;
                    ALTER TABLE {tabla} FORCE ROW LEVEL SECURITY;
                    CREATE POLICY tenant_isolation ON {tabla}
                        USING (tenant_id::text = current_setting('app.tenant_id', true))
                        WITH CHECK (tenant_id::text = current_setting('app.tenant_id', true));
                    GRANT SELECT, INSERT, UPDATE, DELETE ON {tabla} TO propia_app;
                ");
            }

            // Categorias y catalogo: tenant_id nullable (global PropIA + tenant).
            foreach (var tabla in new[] { "reporte_categorias", "reporte_catalogo" })
            {
                migrationBuilder.Sql($@"
                    ALTER TABLE {tabla} ENABLE ROW LEVEL SECURITY;
                    ALTER TABLE {tabla} FORCE ROW LEVEL SECURITY;
                    CREATE POLICY tenant_or_global ON {tabla}
                        USING (tenant_id IS NULL
                               OR tenant_id::text = current_setting('app.tenant_id', true))
                        WITH CHECK (tenant_id IS NULL
                                    OR tenant_id::text = current_setting('app.tenant_id', true));
                    GRANT SELECT, INSERT, UPDATE ON {tabla} TO propia_app;
                ");
            }

            // -----------------------------------------------------------------
            // Seed: 8 categorias base PropIA + ~25 reportes (spec seccion 4.2)
            // -----------------------------------------------------------------

            // Generamos guids deterministicos para poder referenciar las categorias
            // desde los inserts de reporte_catalogo.
            migrationBuilder.Sql(@"
                INSERT INTO reporte_categorias (id, tenant_id, nombre, icono, color, modulo_origen, orden, es_activa, created_at, created_by)
                VALUES
                    ('aaaaaaaa-0001-4000-8000-000000000001', NULL, 'Financiero',         'fi-rr-chart-pie',        '#22c55e', '2.6',  1, true, now(), NULL),
                    ('aaaaaaaa-0001-4000-8000-000000000002', NULL, 'Cartera',            'fi-rr-wallet',           '#f59e0b', '2.7',  2, true, now(), NULL),
                    ('aaaaaaaa-0001-4000-8000-000000000003', NULL, 'PQRSD y Convivencia','fi-rr-comment',          '#14b8a6', '2.9',  3, true, now(), NULL),
                    ('aaaaaaaa-0001-4000-8000-000000000004', NULL, 'Gestion Operativa',  'fi-rr-list-check',       '#6366f1', '2.10', 4, true, now(), NULL),
                    ('aaaaaaaa-0001-4000-8000-000000000005', NULL, 'Mantenimiento',      'fi-rr-wrench-simple',    '#a855f7', '2.11', 5, true, now(), NULL),
                    ('aaaaaaaa-0001-4000-8000-000000000006', NULL, 'Comunicaciones',     'fi-rr-megaphone',        '#ec4899', '2.14', 6, true, now(), NULL),
                    ('aaaaaaaa-0001-4000-8000-000000000007', NULL, 'Documentos',         'fi-rr-document',         '#0ea5e9', '2.15', 7, true, now(), NULL),
                    ('aaaaaaaa-0001-4000-8000-000000000008', NULL, 'Otros',              'fi-rr-folder',           '#64748b', 'core', 8, true, now(), NULL);
            ");

            // Reportes base por categoria.
            migrationBuilder.Sql(@"
                INSERT INTO reporte_catalogo (id, tenant_id, categoria_id, nombre, descripcion, modulo_origen, clave, audiencias_json, es_activo, es_sistema, orden, created_at, created_by)
                VALUES
                    -- Financiero (4)
                    (gen_random_uuid(), NULL, 'aaaaaaaa-0001-4000-8000-000000000001', 'Ejecucion presupuestal del periodo',
                        'Presupuestado vs. ejecutado por rubro con % de ejecucion.', '2.6', 'financiero.ejecucion_presupuestal',
                        '[""ADMINISTRADOR"",""CONSEJO""]', true, true, 1, now(), NULL),
                    (gen_random_uuid(), NULL, 'aaaaaaaa-0001-4000-8000-000000000001', 'Recaudo de cuota de administracion',
                        'Evolucion mensual del recaudo con variacion vs. periodo anterior.', '2.6', 'financiero.recaudo',
                        '[""ADMINISTRADOR"",""CONSEJO""]', true, true, 2, now(), NULL),
                    (gen_random_uuid(), NULL, 'aaaaaaaa-0001-4000-8000-000000000001', 'Ingresos por cuotas extraordinarias',
                        'Recaudo vs. meta por cada cuota extraordinaria activa.', '2.6', 'financiero.cuotas_extraordinarias',
                        '[""ADMINISTRADOR"",""CONSEJO""]', true, true, 3, now(), NULL),
                    (gen_random_uuid(), NULL, 'aaaaaaaa-0001-4000-8000-000000000001', 'Informe financiero para asamblea',
                        'Resumen ejecutivo del periodo: presupuesto + cartera + operacion.', '2.6', 'financiero.informe_asamblea',
                        '[""ADMINISTRADOR"",""CONSEJO"",""PROPIETARIO""]', true, true, 4, now(), NULL),

                    -- Cartera (5)
                    (gen_random_uuid(), NULL, 'aaaaaaaa-0001-4000-8000-000000000002', 'Estado de cartera por aging',
                        'Distribucion de mora por tramos (0-30, 31-60, 61-90, +90 dias).', '2.7', 'cartera.aging',
                        '[""ADMINISTRADOR"",""CONSEJO""]', true, true, 1, now(), NULL),
                    (gen_random_uuid(), NULL, 'aaaaaaaa-0001-4000-8000-000000000002', 'Cartera por unidad privada',
                        'Detalle de deuda por unidad con estado y dias de mora.', '2.7', 'cartera.por_unidad',
                        '[""ADMINISTRADOR""]', true, true, 2, now(), NULL),
                    (gen_random_uuid(), NULL, 'aaaaaaaa-0001-4000-8000-000000000002', 'Evolucion de la mora',
                        'Tendencia de la cartera en mora en el periodo seleccionado.', '2.7', 'cartera.evolucion',
                        '[""ADMINISTRADOR"",""CONSEJO""]', true, true, 3, now(), NULL),
                    (gen_random_uuid(), NULL, 'aaaaaaaa-0001-4000-8000-000000000002', 'Acuerdos de pago activos',
                        'Acuerdos vigentes con cumplimiento y saldo pendiente.', '2.7', 'cartera.acuerdos_activos',
                        '[""ADMINISTRADOR""]', true, true, 4, now(), NULL),
                    (gen_random_uuid(), NULL, 'aaaaaaaa-0001-4000-8000-000000000002', 'Paz y salvos emitidos',
                        'Registro de paz y salvos generados en el periodo.', '2.7', 'cartera.paz_salvos',
                        '[""ADMINISTRADOR""]', true, true, 5, now(), NULL),

                    -- PQRSD (4)
                    (gen_random_uuid(), NULL, 'aaaaaaaa-0001-4000-8000-000000000003', 'Resumen de PQRSD del periodo',
                        'Radicados, resueltos, vencidos y en tramite por tipo.', '2.9', 'pqrsd.resumen',
                        '[""ADMINISTRADOR"",""CONSEJO""]', true, true, 1, now(), NULL),
                    (gen_random_uuid(), NULL, 'aaaaaaaa-0001-4000-8000-000000000003', 'Tiempos de respuesta',
                        'Promedio de dias de resolucion por tipo de PQRSD vs. plazo legal.', '2.9', 'pqrsd.tiempos_respuesta',
                        '[""ADMINISTRADOR"",""CONSEJO""]', true, true, 2, now(), NULL),
                    (gen_random_uuid(), NULL, 'aaaaaaaa-0001-4000-8000-000000000003', 'PQRSD por categoria',
                        'Distribucion por tipo (PQR, Sugerencia, Denuncia).', '2.9', 'pqrsd.por_categoria',
                        '[""ADMINISTRADOR""]', true, true, 3, now(), NULL),
                    (gen_random_uuid(), NULL, 'aaaaaaaa-0001-4000-8000-000000000003', 'Felicitaciones recibidas',
                        'Registro de felicitaciones como indicador positivo de gestion.', '2.9', 'pqrsd.felicitaciones',
                        '[""ADMINISTRADOR""]', true, true, 4, now(), NULL),

                    -- Gestion Operativa (4)
                    (gen_random_uuid(), NULL, 'aaaaaaaa-0001-4000-8000-000000000004', 'Resumen operativo del periodo',
                        'Tareas creadas, completadas, vencidas y canceladas.', '2.10', 'operativo.resumen',
                        '[""ADMINISTRADOR"",""CONSEJO""]', true, true, 1, now(), NULL),
                    (gen_random_uuid(), NULL, 'aaaaaaaa-0001-4000-8000-000000000004', 'Carga de trabajo por responsable',
                        'Distribucion de tareas activas y completadas por persona asignada.', '2.10', 'operativo.carga_responsable',
                        '[""ADMINISTRADOR""]', true, true, 2, now(), NULL),
                    (gen_random_uuid(), NULL, 'aaaaaaaa-0001-4000-8000-000000000004', 'Tiempo promedio de cierre',
                        'Desde creacion hasta completada, filtrable por origen y prioridad.', '2.10', 'operativo.tiempo_cierre',
                        '[""ADMINISTRADOR""]', true, true, 3, now(), NULL),
                    (gen_random_uuid(), NULL, 'aaaaaaaa-0001-4000-8000-000000000004', 'Avance de proyectos activos',
                        '% de completitud de cada proyecto con tareas pendientes.', '2.10', 'operativo.avance_proyectos',
                        '[""ADMINISTRADOR"",""CONSEJO""]', true, true, 4, now(), NULL),

                    -- Mantenimiento (4) - dependen de 2.11 mergeado en main
                    (gen_random_uuid(), NULL, 'aaaaaaaa-0001-4000-8000-000000000005', 'Intervenciones del periodo',
                        'Preventivos y correctivos ejecutados, por activo y proveedor.', '2.11', 'mantenimiento.intervenciones',
                        '[""ADMINISTRADOR"",""CONSEJO""]', true, true, 1, now(), NULL),
                    (gen_random_uuid(), NULL, 'aaaaaaaa-0001-4000-8000-000000000005', 'Activos con mantenimiento vencido',
                        'Inventario de equipos con preventivo vencido o proximo.', '2.11', 'mantenimiento.activos_vencidos',
                        '[""ADMINISTRADOR"",""CONSEJO""]', true, true, 2, now(), NULL),
                    (gen_random_uuid(), NULL, 'aaaaaaaa-0001-4000-8000-000000000005', 'Costo acumulado de mantenimiento',
                        'Gasto ejecutado en mantenimiento por activo y por periodo.', '2.11', 'mantenimiento.costos',
                        '[""ADMINISTRADOR""]', true, true, 3, now(), NULL),
                    (gen_random_uuid(), NULL, 'aaaaaaaa-0001-4000-8000-000000000005', 'Tiempo medio de resolucion (MTTR)',
                        'Promedio entre reporte de falla y cierre del correctivo.', '2.11', 'mantenimiento.mttr',
                        '[""ADMINISTRADOR""]', true, true, 4, now(), NULL),

                    -- Comunicaciones (3)
                    (gen_random_uuid(), NULL, 'aaaaaaaa-0001-4000-8000-000000000006', 'Resumen de comunicados enviados',
                        'Total por tipo (Circular, Aviso, Anuncio, Urgente) en el periodo.', '2.14', 'comunicaciones.resumen',
                        '[""ADMINISTRADOR""]', true, true, 1, now(), NULL),
                    (gen_random_uuid(), NULL, 'aaaaaaaa-0001-4000-8000-000000000006', 'Tasa de apertura por comunicado',
                        '% de acuse de recibo por comunicado con acuse activo.', '2.14', 'comunicaciones.apertura',
                        '[""ADMINISTRADOR"",""CONSEJO""]', true, true, 2, now(), NULL),
                    (gen_random_uuid(), NULL, 'aaaaaaaa-0001-4000-8000-000000000006', 'Eficiencia de comunicacion',
                        'Tiempo promedio entre creacion y envio de comunicados.', '2.14', 'comunicaciones.eficiencia',
                        '[""ADMINISTRADOR""]', true, true, 3, now(), NULL),

                    -- Documentos (1)
                    (gen_random_uuid(), NULL, 'aaaaaaaa-0001-4000-8000-000000000007', 'Resumen del repositorio documental',
                        'Total de documentos, nuevos en el periodo y tamano acumulado.', '2.15', 'documentos.resumen',
                        '[""ADMINISTRADOR""]', true, true, 1, now(), NULL);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reporte_generados");

            migrationBuilder.DropTable(
                name: "reporte_programacion_destinatarios");

            migrationBuilder.DropTable(
                name: "reporte_semaforo_config");

            migrationBuilder.DropTable(
                name: "reporte_programaciones");

            migrationBuilder.DropTable(
                name: "reporte_catalogo");

            migrationBuilder.DropTable(
                name: "reporte_categorias");
        }
    }
}
