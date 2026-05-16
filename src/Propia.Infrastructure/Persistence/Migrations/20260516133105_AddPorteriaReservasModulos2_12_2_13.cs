using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPorteriaReservasModulos2_12_2_13 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "autorizaciones_previa",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidad_privada_id = table.Column<Guid>(type: "uuid", nullable: false),
                    creado_por_persona_id = table.Column<Guid>(type: "uuid", nullable: false),
                    origen = table.Column<int>(type: "integer", nullable: false),
                    nombre_visitante = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    documento_visitante = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    tipo_visitante = table.Column<int>(type: "integer", nullable: false),
                    vigencia_inicio = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    vigencia_fin = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    nota_portero = table.Column<string>(type: "text", nullable: true),
                    estado = table.Column<int>(type: "integer", nullable: false),
                    usos_maximos = table.Column<int>(type: "integer", nullable: false),
                    usos_realizados = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_autorizaciones_previa", x => x.id);
                    table.ForeignKey(
                        name: "FK_autorizaciones_previa_personas_creado_por_persona_id",
                        column: x => x.creado_por_persona_id,
                        principalTable: "personas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_autorizaciones_previa_unidades_privadas_unidad_privada_id",
                        column: x => x.unidad_privada_id,
                        principalTable: "unidades_privadas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "porteria_configuracion",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    destino_visitante_obligatorio = table.Column<bool>(type: "boolean", nullable: false),
                    canal_notificacion_paquetes = table.Column<int>(type: "integer", nullable: false),
                    umbral_paquete_amarillo_min = table.Column<int>(type: "integer", nullable: false),
                    umbral_paquete_rojo_min = table.Column<int>(type: "integer", nullable: false),
                    autoregistro_qr_activo = table.Column<bool>(type: "boolean", nullable: false),
                    genera_tarea_desde_novedad = table.Column<bool>(type: "boolean", nullable: false),
                    retencion_datos_visitantes_dias = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_porteria_configuracion", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "reservas_recurrentes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    zona_comun_id = table.Column<Guid>(type: "uuid", nullable: false),
                    persona_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidad_privada_id = table.Column<Guid>(type: "uuid", nullable: false),
                    frecuencia = table.Column<int>(type: "integer", nullable: false),
                    hora_inicio = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    hora_fin = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    fecha_inicio = table.Column<DateOnly>(type: "date", nullable: false),
                    fecha_fin = table.Column<DateOnly>(type: "date", nullable: true),
                    total_ocurrencias = table.Column<int>(type: "integer", nullable: true),
                    estado = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reservas_recurrentes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "turnos_porteria",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    guarda_persona_id = table.Column<Guid>(type: "uuid", nullable: false),
                    punto_acceso = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    estado = table.Column<int>(type: "integer", nullable: false),
                    fecha_apertura = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    fecha_cierre = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    nota_cierre = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_turnos_porteria", x => x.id);
                    table.ForeignKey(
                        name: "FK_turnos_porteria_personas_guarda_persona_id",
                        column: x => x.guarda_persona_id,
                        principalTable: "personas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "vehiculos_autorizados",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidad_privada_id = table.Column<Guid>(type: "uuid", nullable: false),
                    placa = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    marca = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    modelo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    color = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    parqueadero_asignado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehiculos_autorizados", x => x.id);
                    table.ForeignKey(
                        name: "FK_vehiculos_autorizados_unidades_privadas_unidad_privada_id",
                        column: x => x.unidad_privada_id,
                        principalTable: "unidades_privadas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "visitantes_frecuentes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    documento = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    tipo_documento = table.Column<int>(type: "integer", nullable: true),
                    foto_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    aviso_tratamiento_aceptado = table.Column<bool>(type: "boolean", nullable: false),
                    aviso_aceptado_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ultimo_ingreso_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    total_visitas = table.Column<int>(type: "integer", nullable: false),
                    purga_programada_at = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_visitantes_frecuentes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "zona_bloqueos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    zona_comun_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    fecha_inicio = table.Column<DateOnly>(type: "date", nullable: false),
                    fecha_fin = table.Column<DateOnly>(type: "date", nullable: false),
                    hora_inicio = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    hora_fin = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    etiqueta = table.Column<int>(type: "integer", nullable: false),
                    motivo_personalizado = table.Column<string>(type: "text", nullable: true),
                    visible_para_residentes = table.Column<bool>(type: "boolean", nullable: false),
                    origen = table.Column<int>(type: "integer", nullable: false),
                    creado_por_persona_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_zona_bloqueos", x => x.id);
                    table.ForeignKey(
                        name: "FK_zona_bloqueos_zonas_comunes_zona_comun_id",
                        column: x => x.zona_comun_id,
                        principalTable: "zonas_comunes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "zona_config_reserva",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    zona_comun_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requiere_aprobacion = table.Column<bool>(type: "boolean", nullable: false),
                    max_reservas_activas_por_unidad = table.Column<int>(type: "integer", nullable: true),
                    bloqueo_por_cartera = table.Column<bool>(type: "boolean", nullable: false),
                    anticipacion_minima_horas = table.Column<int>(type: "integer", nullable: false),
                    anticipacion_maxima_dias = table.Column<int>(type: "integer", nullable: false),
                    duracion_minima_minutos = table.Column<int>(type: "integer", nullable: false),
                    duracion_maxima_minutos = table.Column<int>(type: "integer", nullable: false),
                    intervalo_bloque_minutos = table.Column<int>(type: "integer", nullable: false),
                    tiene_tarifa = table.Column<bool>(type: "boolean", nullable: false),
                    modalidad_cobro = table.Column<int>(type: "integer", nullable: false),
                    valor_tarifa = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    politica_reembolso = table.Column<int>(type: "integer", nullable: false),
                    valor_penalidad_cancelacion = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    limite_cancelacion_horas = table.Column<int>(type: "integer", nullable: false),
                    permite_cancelacion_residente = table.Column<bool>(type: "boolean", nullable: false),
                    cancelacion_tardia = table.Column<int>(type: "integer", nullable: false),
                    reglamento_texto = table.Column<string>(type: "text", nullable: true),
                    reglamento_archivo_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    requiere_aceptacion_reglamento = table.Column<bool>(type: "boolean", nullable: false),
                    notif_confirmacion_whatsapp = table.Column<bool>(type: "boolean", nullable: false),
                    notif_confirmacion_inapp = table.Column<bool>(type: "boolean", nullable: false),
                    notif_recordatorio_whatsapp = table.Column<bool>(type: "boolean", nullable: false),
                    notif_recordatorio_horas_antes = table.Column<int>(type: "integer", nullable: false),
                    motivo_bloqueo_visible = table.Column<bool>(type: "boolean", nullable: false),
                    visible_para_residentes = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_zona_config_reserva", x => x.id);
                    table.ForeignKey(
                        name: "FK_zona_config_reserva_zonas_comunes_zona_comun_id",
                        column: x => x.zona_comun_id,
                        principalTable: "zonas_comunes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "codigos_ingreso",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    autorizacion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo_numerico = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    qr_payload = table.Column<string>(type: "text", nullable: false),
                    estado = table.Column<int>(type: "integer", nullable: false),
                    usado_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_codigos_ingreso", x => x.id);
                    table.ForeignKey(
                        name: "FK_codigos_ingreso_autorizaciones_previa_autorizacion_id",
                        column: x => x.autorizacion_id,
                        principalTable: "autorizaciones_previa",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "correspondencias",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    turno_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidad_privada_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    remitente = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    descripcion = table.Column<string>(type: "text", nullable: true),
                    estado = table.Column<int>(type: "integer", nullable: false),
                    recibido_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    notificado_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    entregado_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    entregado_a = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    devuelto_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    motivo_devolucion = table.Column<string>(type: "text", nullable: true),
                    guarda_recibe_persona_id = table.Column<Guid>(type: "uuid", nullable: false),
                    guarda_entrega_persona_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_correspondencias", x => x.id);
                    table.ForeignKey(
                        name: "FK_correspondencias_turnos_porteria_turno_id",
                        column: x => x.turno_id,
                        principalTable: "turnos_porteria",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_correspondencias_unidades_privadas_unidad_privada_id",
                        column: x => x.unidad_privada_id,
                        principalTable: "unidades_privadas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "novedades_turno",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    turno_id = table.Column<Guid>(type: "uuid", nullable: false),
                    guarda_persona_id = table.Column<Guid>(type: "uuid", nullable: false),
                    descripcion = table.Column<string>(type: "text", nullable: false),
                    genera_tarea = table.Column<bool>(type: "boolean", nullable: false),
                    tarea_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_novedades_turno", x => x.id);
                    table.ForeignKey(
                        name: "FK_novedades_turno_tareas_tarea_id",
                        column: x => x.tarea_id,
                        principalTable: "tareas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_novedades_turno_turnos_porteria_turno_id",
                        column: x => x.turno_id,
                        principalTable: "turnos_porteria",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "registros_vehiculo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    turno_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_evento = table.Column<int>(type: "integer", nullable: false),
                    placa = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    vehiculo_autorizado_id = table.Column<Guid>(type: "uuid", nullable: true),
                    unidad_privada_id = table.Column<Guid>(type: "uuid", nullable: true),
                    es_visita = table.Column<bool>(type: "boolean", nullable: false),
                    conductor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    observacion = table.Column<string>(type: "text", nullable: true),
                    origen_registro = table.Column<int>(type: "integer", nullable: false),
                    timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    guarda_persona_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_registros_vehiculo", x => x.id);
                    table.ForeignKey(
                        name: "FK_registros_vehiculo_turnos_porteria_turno_id",
                        column: x => x.turno_id,
                        principalTable: "turnos_porteria",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_registros_vehiculo_unidades_privadas_unidad_privada_id",
                        column: x => x.unidad_privada_id,
                        principalTable: "unidades_privadas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_registros_vehiculo_vehiculos_autorizados_vehiculo_autorizad~",
                        column: x => x.vehiculo_autorizado_id,
                        principalTable: "vehiculos_autorizados",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "zona_franjas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    zona_config_reserva_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dia_semana = table.Column<int>(type: "integer", nullable: false),
                    hora_apertura = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    hora_cierre = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    activa = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_zona_franjas", x => x.id);
                    table.ForeignKey(
                        name: "FK_zona_franjas_zona_config_reserva_zona_config_reserva_id",
                        column: x => x.zona_config_reserva_id,
                        principalTable: "zona_config_reserva",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "registros_visita",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    turno_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_evento = table.Column<int>(type: "integer", nullable: false),
                    tipo_visitante = table.Column<int>(type: "integer", nullable: false),
                    visitante_frecuente_id = table.Column<Guid>(type: "uuid", nullable: true),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    documento = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    tipo_documento = table.Column<int>(type: "integer", nullable: true),
                    destino_tipo = table.Column<int>(type: "integer", nullable: true),
                    destino_id = table.Column<Guid>(type: "uuid", nullable: true),
                    autorizacion_id = table.Column<Guid>(type: "uuid", nullable: true),
                    codigo_ingreso_id = table.Column<Guid>(type: "uuid", nullable: true),
                    observacion = table.Column<string>(type: "text", nullable: true),
                    timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    guarda_persona_id = table.Column<Guid>(type: "uuid", nullable: false),
                    aviso_tratamiento_confirmado = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_registros_visita", x => x.id);
                    table.ForeignKey(
                        name: "FK_registros_visita_autorizaciones_previa_autorizacion_id",
                        column: x => x.autorizacion_id,
                        principalTable: "autorizaciones_previa",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_registros_visita_codigos_ingreso_codigo_ingreso_id",
                        column: x => x.codigo_ingreso_id,
                        principalTable: "codigos_ingreso",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_registros_visita_turnos_porteria_turno_id",
                        column: x => x.turno_id,
                        principalTable: "turnos_porteria",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_registros_visita_visitantes_frecuentes_visitante_frecuente_~",
                        column: x => x.visitante_frecuente_id,
                        principalTable: "visitantes_frecuentes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "reserva_pagos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reserva_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReservaId1 = table.Column<Guid>(type: "uuid", nullable: true),
                    monto = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    estado_pago = table.Column<int>(type: "integer", nullable: false),
                    wompi_transaction_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    wompi_reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    paid_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reserva_pagos", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "reservas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    zona_comun_id = table.Column<Guid>(type: "uuid", nullable: false),
                    persona_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidad_privada_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    hora_inicio = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    hora_fin = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    estado = table.Column<int>(type: "integer", nullable: false),
                    es_recurrente = table.Column<bool>(type: "boolean", nullable: false),
                    reserva_recurrente_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reglamento_aceptado = table.Column<bool>(type: "boolean", nullable: false),
                    reglamento_aceptado_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    motivo_cancelacion = table.Column<string>(type: "text", nullable: true),
                    cancelada_por_persona_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cancelada_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reserva_pago_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reservas", x => x.id);
                    table.ForeignKey(
                        name: "FK_reservas_personas_persona_id",
                        column: x => x.persona_id,
                        principalTable: "personas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_reservas_reserva_pagos_reserva_pago_id",
                        column: x => x.reserva_pago_id,
                        principalTable: "reserva_pagos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_reservas_reservas_recurrentes_reserva_recurrente_id",
                        column: x => x.reserva_recurrente_id,
                        principalTable: "reservas_recurrentes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_reservas_unidades_privadas_unidad_privada_id",
                        column: x => x.unidad_privada_id,
                        principalTable: "unidades_privadas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_reservas_zonas_comunes_zona_comun_id",
                        column: x => x.zona_comun_id,
                        principalTable: "zonas_comunes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_autorizaciones_previa_creado_por_persona_id",
                table: "autorizaciones_previa",
                column: "creado_por_persona_id");

            migrationBuilder.CreateIndex(
                name: "IX_autorizaciones_previa_tenant_id",
                table: "autorizaciones_previa",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_autorizaciones_previa_tenant_id_estado",
                table: "autorizaciones_previa",
                columns: new[] { "tenant_id", "estado" });

            migrationBuilder.CreateIndex(
                name: "IX_autorizaciones_previa_tenant_id_unidad_privada_id",
                table: "autorizaciones_previa",
                columns: new[] { "tenant_id", "unidad_privada_id" });

            migrationBuilder.CreateIndex(
                name: "IX_autorizaciones_previa_unidad_privada_id",
                table: "autorizaciones_previa",
                column: "unidad_privada_id");

            migrationBuilder.CreateIndex(
                name: "IX_codigos_ingreso_autorizacion_id",
                table: "codigos_ingreso",
                column: "autorizacion_id");

            migrationBuilder.CreateIndex(
                name: "IX_codigos_ingreso_tenant_id",
                table: "codigos_ingreso",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_codigos_ingreso_tenant_id_codigo_numerico",
                table: "codigos_ingreso",
                columns: new[] { "tenant_id", "codigo_numerico" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_correspondencias_tenant_id",
                table: "correspondencias",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_correspondencias_tenant_id_estado",
                table: "correspondencias",
                columns: new[] { "tenant_id", "estado" });

            migrationBuilder.CreateIndex(
                name: "IX_correspondencias_tenant_id_unidad_privada_id_estado",
                table: "correspondencias",
                columns: new[] { "tenant_id", "unidad_privada_id", "estado" });

            migrationBuilder.CreateIndex(
                name: "IX_correspondencias_turno_id",
                table: "correspondencias",
                column: "turno_id");

            migrationBuilder.CreateIndex(
                name: "IX_correspondencias_unidad_privada_id",
                table: "correspondencias",
                column: "unidad_privada_id");

            migrationBuilder.CreateIndex(
                name: "IX_novedades_turno_tarea_id",
                table: "novedades_turno",
                column: "tarea_id");

            migrationBuilder.CreateIndex(
                name: "IX_novedades_turno_tenant_id",
                table: "novedades_turno",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_novedades_turno_turno_id",
                table: "novedades_turno",
                column: "turno_id");

            migrationBuilder.CreateIndex(
                name: "IX_porteria_configuracion_tenant_id",
                table: "porteria_configuracion",
                column: "tenant_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_registros_vehiculo_tenant_id",
                table: "registros_vehiculo",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_registros_vehiculo_tenant_id_timestamp",
                table: "registros_vehiculo",
                columns: new[] { "tenant_id", "timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_registros_vehiculo_turno_id",
                table: "registros_vehiculo",
                column: "turno_id");

            migrationBuilder.CreateIndex(
                name: "IX_registros_vehiculo_unidad_privada_id",
                table: "registros_vehiculo",
                column: "unidad_privada_id");

            migrationBuilder.CreateIndex(
                name: "IX_registros_vehiculo_vehiculo_autorizado_id",
                table: "registros_vehiculo",
                column: "vehiculo_autorizado_id");

            migrationBuilder.CreateIndex(
                name: "IX_registros_visita_autorizacion_id",
                table: "registros_visita",
                column: "autorizacion_id");

            migrationBuilder.CreateIndex(
                name: "IX_registros_visita_codigo_ingreso_id",
                table: "registros_visita",
                column: "codigo_ingreso_id");

            migrationBuilder.CreateIndex(
                name: "IX_registros_visita_tenant_id",
                table: "registros_visita",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_registros_visita_tenant_id_timestamp",
                table: "registros_visita",
                columns: new[] { "tenant_id", "timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_registros_visita_turno_id",
                table: "registros_visita",
                column: "turno_id");

            migrationBuilder.CreateIndex(
                name: "IX_registros_visita_visitante_frecuente_id",
                table: "registros_visita",
                column: "visitante_frecuente_id");

            migrationBuilder.CreateIndex(
                name: "IX_reserva_pagos_ReservaId1",
                table: "reserva_pagos",
                column: "ReservaId1");

            migrationBuilder.CreateIndex(
                name: "IX_reserva_pagos_tenant_id",
                table: "reserva_pagos",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_reservas_persona_id",
                table: "reservas",
                column: "persona_id");

            migrationBuilder.CreateIndex(
                name: "IX_reservas_reserva_pago_id",
                table: "reservas",
                column: "reserva_pago_id");

            migrationBuilder.CreateIndex(
                name: "IX_reservas_reserva_recurrente_id",
                table: "reservas",
                column: "reserva_recurrente_id");

            migrationBuilder.CreateIndex(
                name: "IX_reservas_tenant_id",
                table: "reservas",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_reservas_tenant_id_codigo",
                table: "reservas",
                columns: new[] { "tenant_id", "codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_reservas_tenant_id_fecha_estado",
                table: "reservas",
                columns: new[] { "tenant_id", "fecha", "estado" });

            migrationBuilder.CreateIndex(
                name: "IX_reservas_tenant_id_persona_id_estado",
                table: "reservas",
                columns: new[] { "tenant_id", "persona_id", "estado" });

            migrationBuilder.CreateIndex(
                name: "IX_reservas_tenant_id_zona_comun_id_fecha",
                table: "reservas",
                columns: new[] { "tenant_id", "zona_comun_id", "fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_reservas_unidad_privada_id",
                table: "reservas",
                column: "unidad_privada_id");

            migrationBuilder.CreateIndex(
                name: "IX_reservas_zona_comun_id",
                table: "reservas",
                column: "zona_comun_id");

            migrationBuilder.CreateIndex(
                name: "IX_reservas_recurrentes_tenant_id",
                table: "reservas_recurrentes",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_turnos_porteria_guarda_persona_id",
                table: "turnos_porteria",
                column: "guarda_persona_id");

            migrationBuilder.CreateIndex(
                name: "IX_turnos_porteria_tenant_id",
                table: "turnos_porteria",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_turnos_porteria_tenant_id_guarda_persona_id_punto_acceso_es~",
                table: "turnos_porteria",
                columns: new[] { "tenant_id", "guarda_persona_id", "punto_acceso", "estado" });

            migrationBuilder.CreateIndex(
                name: "IX_vehiculos_autorizados_tenant_id",
                table: "vehiculos_autorizados",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_vehiculos_autorizados_tenant_id_placa",
                table: "vehiculos_autorizados",
                columns: new[] { "tenant_id", "placa" });

            migrationBuilder.CreateIndex(
                name: "IX_vehiculos_autorizados_unidad_privada_id",
                table: "vehiculos_autorizados",
                column: "unidad_privada_id");

            migrationBuilder.CreateIndex(
                name: "IX_visitantes_frecuentes_tenant_id",
                table: "visitantes_frecuentes",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_visitantes_frecuentes_tenant_id_documento",
                table: "visitantes_frecuentes",
                columns: new[] { "tenant_id", "documento" });

            migrationBuilder.CreateIndex(
                name: "IX_zona_bloqueos_tenant_id",
                table: "zona_bloqueos",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_zona_bloqueos_tenant_id_zona_comun_id_fecha_inicio_fecha_fin",
                table: "zona_bloqueos",
                columns: new[] { "tenant_id", "zona_comun_id", "fecha_inicio", "fecha_fin" });

            migrationBuilder.CreateIndex(
                name: "IX_zona_bloqueos_zona_comun_id",
                table: "zona_bloqueos",
                column: "zona_comun_id");

            migrationBuilder.CreateIndex(
                name: "IX_zona_config_reserva_tenant_id",
                table: "zona_config_reserva",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_zona_config_reserva_tenant_id_zona_comun_id",
                table: "zona_config_reserva",
                columns: new[] { "tenant_id", "zona_comun_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_zona_config_reserva_zona_comun_id",
                table: "zona_config_reserva",
                column: "zona_comun_id");

            migrationBuilder.CreateIndex(
                name: "IX_zona_franjas_tenant_id",
                table: "zona_franjas",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_zona_franjas_zona_config_reserva_id_dia_semana",
                table: "zona_franjas",
                columns: new[] { "zona_config_reserva_id", "dia_semana" });

            migrationBuilder.AddForeignKey(
                name: "FK_reserva_pagos_reservas_ReservaId1",
                table: "reserva_pagos",
                column: "ReservaId1",
                principalTable: "reservas",
                principalColumn: "id");

            // -----------------------------------------------------------------
            // RLS + GRANTs (Spec 2.12 y 2.13)
            // -----------------------------------------------------------------

            foreach (var tabla in new[] {
                // 2.12 Porteria
                "turnos_porteria",
                "visitantes_frecuentes",
                "autorizaciones_previa",
                "codigos_ingreso",
                "registros_visita",
                "vehiculos_autorizados",
                "registros_vehiculo",
                "correspondencias",
                "novedades_turno",
                "porteria_configuracion",
                // 2.13 Reservas
                "zona_config_reserva",
                "zona_franjas",
                "reservas",
                "reservas_recurrentes",
                "reserva_pagos",
                "zona_bloqueos"
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

            // -----------------------------------------------------------------
            // Triggers append-only (Spec 2.12 RN-10)
            // Bloquea UPDATE/DELETE en tablas inmutables de registros.
            // -----------------------------------------------------------------

            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION porteria_append_only()
                RETURNS TRIGGER AS $$
                BEGIN
                    RAISE EXCEPTION 'tabla append-only (spec 2.12 RN-10). No se permite % sobre id %.', TG_OP, OLD.id;
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER registros_visita_no_update
                    BEFORE UPDATE ON registros_visita
                    FOR EACH ROW EXECUTE FUNCTION porteria_append_only();

                CREATE TRIGGER registros_visita_no_delete
                    BEFORE DELETE ON registros_visita
                    FOR EACH ROW EXECUTE FUNCTION porteria_append_only();

                CREATE TRIGGER registros_vehiculo_no_update
                    BEFORE UPDATE ON registros_vehiculo
                    FOR EACH ROW EXECUTE FUNCTION porteria_append_only();

                CREATE TRIGGER registros_vehiculo_no_delete
                    BEFORE DELETE ON registros_vehiculo
                    FOR EACH ROW EXECUTE FUNCTION porteria_append_only();
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP TRIGGER IF EXISTS registros_vehiculo_no_delete ON registros_vehiculo;
                DROP TRIGGER IF EXISTS registros_vehiculo_no_update ON registros_vehiculo;
                DROP TRIGGER IF EXISTS registros_visita_no_delete ON registros_visita;
                DROP TRIGGER IF EXISTS registros_visita_no_update ON registros_visita;
                DROP FUNCTION IF EXISTS porteria_append_only();
            ");

            migrationBuilder.DropForeignKey(
                name: "FK_reserva_pagos_reservas_ReservaId1",
                table: "reserva_pagos");

            migrationBuilder.DropTable(
                name: "correspondencias");

            migrationBuilder.DropTable(
                name: "novedades_turno");

            migrationBuilder.DropTable(
                name: "porteria_configuracion");

            migrationBuilder.DropTable(
                name: "registros_vehiculo");

            migrationBuilder.DropTable(
                name: "registros_visita");

            migrationBuilder.DropTable(
                name: "zona_bloqueos");

            migrationBuilder.DropTable(
                name: "zona_franjas");

            migrationBuilder.DropTable(
                name: "vehiculos_autorizados");

            migrationBuilder.DropTable(
                name: "codigos_ingreso");

            migrationBuilder.DropTable(
                name: "turnos_porteria");

            migrationBuilder.DropTable(
                name: "visitantes_frecuentes");

            migrationBuilder.DropTable(
                name: "zona_config_reserva");

            migrationBuilder.DropTable(
                name: "autorizaciones_previa");

            migrationBuilder.DropTable(
                name: "reservas");

            migrationBuilder.DropTable(
                name: "reserva_pagos");

            migrationBuilder.DropTable(
                name: "reservas_recurrentes");
        }
    }
}
