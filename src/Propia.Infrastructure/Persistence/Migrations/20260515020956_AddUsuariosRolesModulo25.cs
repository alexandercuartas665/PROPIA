using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUsuariosRolesModulo25 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "fecha_revocacion",
                table: "usuarios_tenant",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "motivo_revocacion",
                table: "usuarios_tenant",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "rol_id",
                table: "usuarios_tenant",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "acceso_auditorias",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tipo_evento = table.Column<int>(type: "integer", nullable: false),
                    actor_usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    entidad_afectada_id = table.Column<Guid>(type: "uuid", nullable: true),
                    detalle = table.Column<string>(type: "text", nullable: true),
                    ip_origen = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    dispositivo = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    canal = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_acceso_auditorias", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "roles_copropiedad",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    es_eliminable = table.Column<bool>(type: "boolean", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    copiado_de_rol_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles_copropiedad", x => x.id);
                    table.ForeignKey(
                        name: "FK_roles_copropiedad_roles_copropiedad_copiado_de_rol_id",
                        column: x => x.copiado_de_rol_id,
                        principalTable: "roles_copropiedad",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "usuario_auth_metodos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    proveedor_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usuario_auth_metodos", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "usuario_sesiones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    token_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    dispositivo = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ip_origen = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    canal_auth = table.Column<int>(type: "integer", nullable: false),
                    activa = table.Column<bool>(type: "boolean", nullable: false),
                    ultimo_uso_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expira_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usuario_sesiones", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rol_permisos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    rol_id = table.Column<Guid>(type: "uuid", nullable: false),
                    modulo_codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    accion = table.Column<int>(type: "integer", nullable: false),
                    habilitado = table.Column<bool>(type: "boolean", nullable: false),
                    nivel_dato = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rol_permisos", x => x.id);
                    table.ForeignKey(
                        name: "FK_rol_permisos_roles_copropiedad_rol_id",
                        column: x => x.rol_id,
                        principalTable: "roles_copropiedad",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "usuario_invitaciones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    persona_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rol_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    estado = table.Column<int>(type: "integer", nullable: false),
                    expira_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    aceptada_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancelada_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    canal_envio = table.Column<int>(type: "integer", nullable: false),
                    creada_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usuario_invitaciones", x => x.id);
                    table.ForeignKey(
                        name: "FK_usuario_invitaciones_personas_persona_id",
                        column: x => x.persona_id,
                        principalTable: "personas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_usuario_invitaciones_roles_copropiedad_rol_id",
                        column: x => x.rol_id,
                        principalTable: "roles_copropiedad",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_usuarios_tenant_rol_id",
                table: "usuarios_tenant",
                column: "rol_id");

            migrationBuilder.CreateIndex(
                name: "IX_acceso_auditorias_created_at",
                table: "acceso_auditorias",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_acceso_auditorias_tenant_id",
                table: "acceso_auditorias",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_acceso_auditorias_tipo_evento",
                table: "acceso_auditorias",
                column: "tipo_evento");

            migrationBuilder.CreateIndex(
                name: "IX_acceso_auditorias_usuario_id",
                table: "acceso_auditorias",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_rol_permisos_rol_id_modulo_codigo_accion",
                table: "rol_permisos",
                columns: new[] { "rol_id", "modulo_codigo", "accion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_roles_copropiedad_copiado_de_rol_id",
                table: "roles_copropiedad",
                column: "copiado_de_rol_id");

            migrationBuilder.CreateIndex(
                name: "IX_roles_copropiedad_tenant_id_nombre",
                table: "roles_copropiedad",
                columns: new[] { "tenant_id", "nombre" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_usuario_auth_metodos_usuario_id_tipo",
                table: "usuario_auth_metodos",
                columns: new[] { "usuario_id", "tipo" });

            migrationBuilder.CreateIndex(
                name: "IX_usuario_invitaciones_persona_id",
                table: "usuario_invitaciones",
                column: "persona_id");

            migrationBuilder.CreateIndex(
                name: "IX_usuario_invitaciones_rol_id",
                table: "usuario_invitaciones",
                column: "rol_id");

            migrationBuilder.CreateIndex(
                name: "IX_usuario_invitaciones_tenant_id",
                table: "usuario_invitaciones",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_usuario_invitaciones_tenant_id_estado",
                table: "usuario_invitaciones",
                columns: new[] { "tenant_id", "estado" });

            migrationBuilder.CreateIndex(
                name: "IX_usuario_invitaciones_token",
                table: "usuario_invitaciones",
                column: "token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_usuario_sesiones_token_hash",
                table: "usuario_sesiones",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_usuario_sesiones_usuario_id",
                table: "usuario_sesiones",
                column: "usuario_id");

            migrationBuilder.AddForeignKey(
                name: "FK_usuarios_tenant_roles_copropiedad_rol_id",
                table: "usuarios_tenant",
                column: "rol_id",
                principalTable: "roles_copropiedad",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            // ============================ RLS + grants ============================
            // roles_copropiedad: visible globales (tenant_id NULL) + del tenant activo.
            // usuario_invitaciones: solo del tenant.
            // rol_permisos: sin RLS directo - se accede via rol_id (cuyo rol ya esta filtrado).
            // usuario_auth_metodos / usuario_sesiones: global (filtran por usuario_id).
            // acceso_auditorias: usuario consulta lo del tenant (RLS filtra por tenant_id).
            migrationBuilder.Sql(@"
                ALTER TABLE roles_copropiedad ENABLE ROW LEVEL SECURITY;
                ALTER TABLE roles_copropiedad FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON roles_copropiedad
                    USING (tenant_id IS NULL OR tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id IS NULL OR tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON roles_copropiedad TO propia_app;

                ALTER TABLE usuario_invitaciones ENABLE ROW LEVEL SECURITY;
                ALTER TABLE usuario_invitaciones FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON usuario_invitaciones
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON usuario_invitaciones TO propia_app;

                ALTER TABLE acceso_auditorias ENABLE ROW LEVEL SECURITY;
                ALTER TABLE acceso_auditorias FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON acceso_auditorias
                    USING (tenant_id IS NULL OR tenant_id = current_tenant_id())
                    WITH CHECK (true);  -- inserts permitidos siempre (incluso sin tenant - logout, login fallido)
                GRANT SELECT, INSERT ON acceso_auditorias TO propia_app;

                GRANT SELECT, INSERT, UPDATE, DELETE ON rol_permisos TO propia_app;
                GRANT SELECT, INSERT, UPDATE, DELETE ON usuario_auth_metodos TO propia_app;
                GRANT SELECT, INSERT, UPDATE, DELETE ON usuario_sesiones TO propia_app;
            ");

            // ============================ Trigger append-only en acceso_auditorias (RN-14) ============================
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION acceso_auditoria_append_only()
                RETURNS TRIGGER AS $$
                BEGIN
                    RAISE EXCEPTION 'acceso_auditorias es append-only: % no permitido (RN-14)', TG_OP;
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER acceso_auditoria_no_update
                    BEFORE UPDATE ON acceso_auditorias
                    FOR EACH ROW EXECUTE FUNCTION acceso_auditoria_append_only();

                CREATE TRIGGER acceso_auditoria_no_delete
                    BEFORE DELETE ON acceso_auditorias
                    FOR EACH ROW EXECUTE FUNCTION acceso_auditoria_append_only();
            ");

            // ============================ Seed roles BASE + EXTENDIDOS + permisos default ============================
            // Roles BASE no eliminables (RN-03) - 5 roles globales.
            // Roles EXTENDIDOS predefinidos, desactivables (RN-04) - 6 roles globales.
            // Permisos por defecto: Administrador todo + el resto segun spec.
            migrationBuilder.Sql(@"
                INSERT INTO roles_copropiedad (id, nombre, descripcion, tipo, es_eliminable, activo, tenant_id, created_at) VALUES
                -- BASE (5)
                ('a0000000-0000-0000-0000-000000000001', 'Administrador', 'Gestiona la PH operativamente. Acceso completo a todos los modulos.', 2, false, true, NULL, now()),
                ('a0000000-0000-0000-0000-000000000002', 'Consejero', 'Miembro del Consejo de Administracion. Supervision con lectura de metricas.', 2, false, true, NULL, now()),
                ('a0000000-0000-0000-0000-000000000003', 'Propietario', 'Titular del inmueble. Portal propio + acciones de titularidad (asamblea, documentos legales).', 2, false, true, NULL, now()),
                ('a0000000-0000-0000-0000-000000000004', 'Residente', 'Persona que habita la unidad. Portal propio - PQRS, reservas, estado de cuenta.', 2, false, true, NULL, now()),
                ('a0000000-0000-0000-0000-000000000005', 'Operario', 'Personal de servicios o mantenimiento. Solo tareas asignadas.', 2, false, true, NULL, now()),
                -- EXTENDIDOS (6) - activo=false por defecto, se activan en cada copropiedad
                ('b0000000-0000-0000-0000-000000000001', 'Coordinador', 'Apoyo operativo con acceso amplio - sin permisos financieros completos.', 3, true, false, NULL, now()),
                ('b0000000-0000-0000-0000-000000000002', 'Contador', 'Acceso a modulos financieros - solo lectura por defecto.', 3, true, false, NULL, now()),
                ('b0000000-0000-0000-0000-000000000003', 'Revisor Fiscal', 'Acceso de auditoria - lectura en finanzas y documentos.', 3, true, false, NULL, now()),
                ('b0000000-0000-0000-0000-000000000004', 'Asistente', 'Apoyo administrativo - PQRS, tareas, directorio.', 3, true, false, NULL, now()),
                ('b0000000-0000-0000-0000-000000000005', 'Vigilante de Seguridad', 'Porteria, directorio basico, correspondencia.', 3, true, false, NULL, now()),
                ('b0000000-0000-0000-0000-000000000006', 'Inmobiliaria', 'Acceso restringido a unidades bajo su gestion.', 3, true, false, NULL, now())
                ON CONFLICT DO NOTHING;
            ");

            // Permisos default: Administrador habilita TODO en todos los modulos al nivel COPROPIEDAD.
            // El resto: solo Ver para empezar; el admin de cada PH afinara segun reglamento.
            // Modulos: DASHBOARD, MI_COPROPIEDAD, DIRECTORIO, USUARIOS_ACCESOS, PRESUPUESTO, CARTERA,
            //          ASAMBLEAS, PQRS, TAREAS, MANTENIMIENTO, PORTERIA, RESERVAS, COMUNICACIONES, DOCUMENTOS, REPORTES
            // Acciones: 1=Ver, 2=Crear, 3=Editar, 4=Eliminar, 5=Aprobar, 6=Exportar
            // NivelDato: 0=SinAcceso, 1=Propio, 2=Copropiedad
            migrationBuilder.Sql(@"
                -- Administrador: TODO habilitado a nivel Copropiedad (2) en todas las acciones
                INSERT INTO rol_permisos (id, rol_id, modulo_codigo, accion, habilitado, nivel_dato, created_at)
                SELECT gen_random_uuid(), 'a0000000-0000-0000-0000-000000000001', m.codigo, a.accion, true, 2, now()
                FROM (VALUES ('DASHBOARD'),('MI_COPROPIEDAD'),('DIRECTORIO'),('USUARIOS_ACCESOS'),('PRESUPUESTO'),
                             ('CARTERA'),('ASAMBLEAS'),('PQRS'),('TAREAS'),('MANTENIMIENTO'),
                             ('PORTERIA'),('RESERVAS'),('COMUNICACIONES'),('DOCUMENTOS'),('REPORTES')) m(codigo)
                CROSS JOIN (VALUES (1),(2),(3),(4),(5),(6)) a(accion)
                ON CONFLICT DO NOTHING;

                -- Consejero: Ver en todo, sin acciones de escritura por defecto
                INSERT INTO rol_permisos (id, rol_id, modulo_codigo, accion, habilitado, nivel_dato, created_at)
                SELECT gen_random_uuid(), 'a0000000-0000-0000-0000-000000000002', m.codigo, 1, true, 2, now()
                FROM (VALUES ('DASHBOARD'),('MI_COPROPIEDAD'),('DIRECTORIO'),('PRESUPUESTO'),
                             ('CARTERA'),('ASAMBLEAS'),('PQRS'),('TAREAS'),('MANTENIMIENTO'),
                             ('PORTERIA'),('RESERVAS'),('COMUNICACIONES'),('DOCUMENTOS'),('REPORTES')) m(codigo)
                ON CONFLICT DO NOTHING;

                -- Propietario: Ver a nivel Propio (1) en operativas + asambleas
                INSERT INTO rol_permisos (id, rol_id, modulo_codigo, accion, habilitado, nivel_dato, created_at)
                SELECT gen_random_uuid(), 'a0000000-0000-0000-0000-000000000003', m.codigo, 1, true, 1, now()
                FROM (VALUES ('DASHBOARD'),('MI_COPROPIEDAD'),('CARTERA'),('ASAMBLEAS'),
                             ('PQRS'),('RESERVAS'),('COMUNICACIONES'),('DOCUMENTOS')) m(codigo)
                ON CONFLICT DO NOTHING;
                -- Propietario: Crear PQRS y Reservas a nivel Propio
                INSERT INTO rol_permisos (id, rol_id, modulo_codigo, accion, habilitado, nivel_dato, created_at) VALUES
                (gen_random_uuid(), 'a0000000-0000-0000-0000-000000000003', 'PQRS', 2, true, 1, now()),
                (gen_random_uuid(), 'a0000000-0000-0000-0000-000000000003', 'RESERVAS', 2, true, 1, now())
                ON CONFLICT DO NOTHING;

                -- Residente: Ver a nivel Propio en operativas (sin asambleas ni doc legales)
                INSERT INTO rol_permisos (id, rol_id, modulo_codigo, accion, habilitado, nivel_dato, created_at)
                SELECT gen_random_uuid(), 'a0000000-0000-0000-0000-000000000004', m.codigo, 1, true, 1, now()
                FROM (VALUES ('DASHBOARD'),('CARTERA'),('PQRS'),('RESERVAS'),('COMUNICACIONES')) m(codigo)
                ON CONFLICT DO NOTHING;
                INSERT INTO rol_permisos (id, rol_id, modulo_codigo, accion, habilitado, nivel_dato, created_at) VALUES
                (gen_random_uuid(), 'a0000000-0000-0000-0000-000000000004', 'PQRS', 2, true, 1, now())
                ON CONFLICT DO NOTHING;

                -- Operario: Solo Tareas (Ver + Editar a nivel propio)
                INSERT INTO rol_permisos (id, rol_id, modulo_codigo, accion, habilitado, nivel_dato, created_at) VALUES
                (gen_random_uuid(), 'a0000000-0000-0000-0000-000000000005', 'DASHBOARD', 1, true, 1, now()),
                (gen_random_uuid(), 'a0000000-0000-0000-0000-000000000005', 'TAREAS', 1, true, 1, now()),
                (gen_random_uuid(), 'a0000000-0000-0000-0000-000000000005', 'TAREAS', 3, true, 1, now())
                ON CONFLICT DO NOTHING;

                -- Extendidos: dejamos sin permisos seedados - cada copropiedad los activa y los configura.
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP TRIGGER IF EXISTS acceso_auditoria_no_delete ON acceso_auditorias;
                DROP TRIGGER IF EXISTS acceso_auditoria_no_update ON acceso_auditorias;
                DROP FUNCTION IF EXISTS acceso_auditoria_append_only();
                DROP POLICY IF EXISTS tenant_isolation ON acceso_auditorias;
                DROP POLICY IF EXISTS tenant_isolation ON usuario_invitaciones;
                DROP POLICY IF EXISTS tenant_isolation ON roles_copropiedad;
            ");

            migrationBuilder.DropForeignKey(
                name: "FK_usuarios_tenant_roles_copropiedad_rol_id",
                table: "usuarios_tenant");

            migrationBuilder.DropTable(
                name: "acceso_auditorias");

            migrationBuilder.DropTable(
                name: "rol_permisos");

            migrationBuilder.DropTable(
                name: "usuario_auth_metodos");

            migrationBuilder.DropTable(
                name: "usuario_invitaciones");

            migrationBuilder.DropTable(
                name: "usuario_sesiones");

            migrationBuilder.DropTable(
                name: "roles_copropiedad");

            migrationBuilder.DropIndex(
                name: "IX_usuarios_tenant_rol_id",
                table: "usuarios_tenant");

            migrationBuilder.DropColumn(
                name: "fecha_revocacion",
                table: "usuarios_tenant");

            migrationBuilder.DropColumn(
                name: "motivo_revocacion",
                table: "usuarios_tenant");

            migrationBuilder.DropColumn(
                name: "rol_id",
                table: "usuarios_tenant");
        }
    }
}
