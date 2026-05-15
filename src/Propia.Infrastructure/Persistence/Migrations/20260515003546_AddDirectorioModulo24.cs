using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDirectorioModulo24 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "acepto_tratamiento_datos",
                table: "personas",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "canal_aceptacion",
                table: "personas",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "estado_directorio",
                table: "personas",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "fecha_aceptacion_datos",
                table: "personas",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "fecha_nacimiento",
                table: "personas",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "genero",
                table: "personas",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ip_aceptacion",
                table: "personas",
                type: "character varying(45)",
                maxLength: 45,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "perfil_incompleto",
                table: "personas",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "version_politica_datos",
                table: "personas",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "estado_directorio",
                table: "empresas",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "logo_url",
                table: "empresas",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "perfil_incompleto",
                table: "empresas",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "regimen_tributario",
                table: "empresas",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "representante_legal_persona_id",
                table: "empresas",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sector_economico",
                table: "empresas",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sitio_web",
                table: "empresas",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "tipo_empresa",
                table: "empresas",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "directorio_contactos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    entidad_tipo = table.Column<int>(type: "integer", nullable: false),
                    entidad_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    subtipo_label = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    valor = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ciudad = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    departamento = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    es_principal = table.Column<bool>(type: "boolean", nullable: false),
                    visibilidad = table.Column<int>(type: "integer", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_directorio_contactos", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "directorio_vinculos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    entidad_tipo = table.Column<int>(type: "integer", nullable: false),
                    entidad_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha_desde = table.Column<DateOnly>(type: "date", nullable: false),
                    fecha_hasta = table.Column<DateOnly>(type: "date", nullable: true),
                    estado = table.Column<int>(type: "integer", nullable: false),
                    motivo_inactivacion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_directorio_vinculos", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "etiquetas_catalogo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    grupo = table.Column<int>(type: "integer", nullable: false),
                    aplica_a = table.Column<int>(type: "integer", nullable: false),
                    es_base = table.Column<bool>(type: "boolean", nullable: false),
                    tiene_logica_especial = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_etiquetas_catalogo", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "persona_empresas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    persona_id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cargo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    es_representante_legal = table.Column<bool>(type: "boolean", nullable: false),
                    es_contacto_principal = table.Column<bool>(type: "boolean", nullable: false),
                    fecha_desde = table.Column<DateOnly>(type: "date", nullable: true),
                    fecha_hasta = table.Column<DateOnly>(type: "date", nullable: true),
                    estado = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_persona_empresas", x => x.id);
                    table.ForeignKey(
                        name: "FK_persona_empresas_empresas_empresa_id",
                        column: x => x.empresa_id,
                        principalTable: "empresas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_persona_empresas_personas_persona_id",
                        column: x => x.persona_id,
                        principalTable: "personas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "directorio_etiquetas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    vinculo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    etiqueta_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_directorio_etiquetas", x => x.id);
                    table.ForeignKey(
                        name: "FK_directorio_etiquetas_directorio_vinculos_vinculo_id",
                        column: x => x.vinculo_id,
                        principalTable: "directorio_vinculos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_directorio_etiquetas_etiquetas_catalogo_etiqueta_id",
                        column: x => x.etiqueta_id,
                        principalTable: "etiquetas_catalogo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_empresas_representante_legal_persona_id",
                table: "empresas",
                column: "representante_legal_persona_id");

            migrationBuilder.CreateIndex(
                name: "IX_directorio_contactos_entidad_tipo_entidad_id",
                table: "directorio_contactos",
                columns: new[] { "entidad_tipo", "entidad_id" });

            migrationBuilder.CreateIndex(
                name: "IX_directorio_contactos_tenant_id",
                table: "directorio_contactos",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_directorio_etiquetas_etiqueta_id",
                table: "directorio_etiquetas",
                column: "etiqueta_id");

            migrationBuilder.CreateIndex(
                name: "IX_directorio_etiquetas_tenant_id",
                table: "directorio_etiquetas",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_directorio_etiquetas_vinculo_id_etiqueta_id",
                table: "directorio_etiquetas",
                columns: new[] { "vinculo_id", "etiqueta_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_directorio_vinculos_tenant_id",
                table: "directorio_vinculos",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_directorio_vinculos_tenant_id_entidad_tipo_entidad_id",
                table: "directorio_vinculos",
                columns: new[] { "tenant_id", "entidad_tipo", "entidad_id" });

            migrationBuilder.CreateIndex(
                name: "IX_etiquetas_catalogo_tenant_id_codigo",
                table: "etiquetas_catalogo",
                columns: new[] { "tenant_id", "codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_persona_empresas_empresa_id_persona_id_cargo",
                table: "persona_empresas",
                columns: new[] { "empresa_id", "persona_id", "cargo" });

            migrationBuilder.CreateIndex(
                name: "IX_persona_empresas_persona_id",
                table: "persona_empresas",
                column: "persona_id");

            migrationBuilder.CreateIndex(
                name: "IX_persona_empresas_tenant_id",
                table: "persona_empresas",
                column: "tenant_id");

            migrationBuilder.AddForeignKey(
                name: "FK_empresas_personas_representante_legal_persona_id",
                table: "empresas",
                column: "representante_legal_persona_id",
                principalTable: "personas",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            // RLS para tablas TenantEntity del modulo 2.4.
            // etiquetas_catalogo es especial: permite ver las base (tenant_id NULL) + las del tenant.
            migrationBuilder.Sql(@"
                ALTER TABLE etiquetas_catalogo ENABLE ROW LEVEL SECURITY;
                ALTER TABLE etiquetas_catalogo FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON etiquetas_catalogo
                    USING (tenant_id IS NULL OR tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id IS NULL OR tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON etiquetas_catalogo TO propia_app;

                ALTER TABLE directorio_vinculos ENABLE ROW LEVEL SECURITY;
                ALTER TABLE directorio_vinculos FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON directorio_vinculos
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON directorio_vinculos TO propia_app;

                ALTER TABLE directorio_contactos ENABLE ROW LEVEL SECURITY;
                ALTER TABLE directorio_contactos FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON directorio_contactos
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON directorio_contactos TO propia_app;

                ALTER TABLE directorio_etiquetas ENABLE ROW LEVEL SECURITY;
                ALTER TABLE directorio_etiquetas FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON directorio_etiquetas
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON directorio_etiquetas TO propia_app;

                ALTER TABLE persona_empresas ENABLE ROW LEVEL SECURITY;
                ALTER TABLE persona_empresas FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON persona_empresas
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON persona_empresas TO propia_app;
            ");

            // Seed del catalogo base de etiquetas (spec 2.4 seccion 5.1 + 5.2 + 5.3)
            migrationBuilder.Sql(@"
                -- Identidad - Personas
                INSERT INTO etiquetas_catalogo (id, codigo, nombre, grupo, aplica_a, es_base, tiene_logica_especial, tenant_id, activo, created_at) VALUES
                ('11111111-0000-0000-0000-000000000001', 'PROPIETARIO',         'Propietario',          1, 1, true, true,  NULL, true, now()),
                ('11111111-0000-0000-0000-000000000002', 'RESIDENTE',           'Residente',            1, 1, true, false, NULL, true, now()),
                ('11111111-0000-0000-0000-000000000003', 'ARRENDATARIO',        'Arrendatario',         1, 1, true, false, NULL, true, now()),
                ('11111111-0000-0000-0000-000000000004', 'COARRENDATARIO',      'Coarrendatario',       1, 1, true, false, NULL, true, now()),
                ('11111111-0000-0000-0000-000000000005', 'USUARIO_UNIDAD',      'Usuario de unidad',    1, 1, true, false, NULL, true, now()),
                ('11111111-0000-0000-0000-000000000006', 'CONSEJERO',           'Miembro del Consejo',  1, 1, true, true,  NULL, true, now()),
                ('11111111-0000-0000-0000-000000000007', 'REVISOR_FISCAL',      'Revisor Fiscal',       1, 1, true, true,  NULL, true, now()),
                ('11111111-0000-0000-0000-000000000008', 'COMITE_CONVIVENCIA',  'Comite de Convivencia',1, 1, true, true,  NULL, true, now()),
                ('11111111-0000-0000-0000-000000000009', 'ADMIN_DELEGADO',      'Administrador Delegado',1,1, true, true,  NULL, true, now()),
                ('11111111-0000-0000-0000-00000000000a', 'PERSONAL_ADMIN',      'Personal administrativo',1,1,true,false, NULL, true, now()),
                ('11111111-0000-0000-0000-00000000000b', 'VISITANTE_FRECUENTE', 'Visitante frecuente',  1, 1, true, false, NULL, true, now()),
                ('11111111-0000-0000-0000-00000000000c', 'CONTACTO_EMERGENCIA', 'Contacto de emergencia',1,1, true, false, NULL, true, now()),

                -- Cargo - Personas
                ('22222222-0000-0000-0000-000000000001', 'PRESIDENTE_CONSEJO',  'Presidente del Consejo',2,1, true, false, NULL, true, now()),
                ('22222222-0000-0000-0000-000000000002', 'TESORERO',            'Tesorero',             2, 1, true, false, NULL, true, now()),
                ('22222222-0000-0000-0000-000000000003', 'SECRETARIO',          'Secretario',           2, 1, true, false, NULL, true, now()),
                ('22222222-0000-0000-0000-000000000004', 'VOCAL',               'Vocal',                2, 1, true, false, NULL, true, now()),
                ('22222222-0000-0000-0000-000000000005', 'ADMINISTRADOR',       'Administrador',        2, 1, true, false, NULL, true, now()),
                ('22222222-0000-0000-0000-000000000006', 'CONTADOR',            'Contador',             2, 1, true, false, NULL, true, now()),
                ('22222222-0000-0000-0000-000000000007', 'ASISTENTE_ADMIN',     'Asistente administrativo',2,1,true,false,NULL, true, now()),
                ('22222222-0000-0000-0000-000000000008', 'COORDINADOR',         'Coordinador operativo',2, 1, true, false, NULL, true, now()),
                ('22222222-0000-0000-0000-000000000009', 'GUARDA',              'Guarda de seguridad',  2, 1, true, false, NULL, true, now()),
                ('22222222-0000-0000-0000-00000000000a', 'ASEADOR',             'Aseador',              2, 1, true, false, NULL, true, now()),
                ('22222222-0000-0000-0000-00000000000b', 'JARDINERO',           'Jardinero',            2, 1, true, false, NULL, true, now()),
                ('22222222-0000-0000-0000-00000000000c', 'PISCINERO',           'Piscinero',            2, 1, true, false, NULL, true, now()),
                ('22222222-0000-0000-0000-00000000000d', 'TECNICO',             'Tecnico de mantenimiento',2,1,true,false,NULL, true, now()),
                ('22222222-0000-0000-0000-00000000000e', 'PORTERO',             'Portero',              2, 1, true, false, NULL, true, now()),

                -- Identidad - Empresas
                ('33333333-0000-0000-0000-000000000001', 'PROVEEDOR',           'Proveedor',            1, 2, true, false, NULL, true, now()),
                ('33333333-0000-0000-0000-000000000002', 'EMPRESA_ADMIN',       'Empresa administradora',1,2, true, false, NULL, true, now()),
                ('33333333-0000-0000-0000-000000000003', 'VIGILANCIA',          'Vigilancia y seguridad',1,2, true, false, NULL, true, now()),
                ('33333333-0000-0000-0000-000000000004', 'ASEO',                'Aseo y limpieza',      1, 2, true, false, NULL, true, now()),
                ('33333333-0000-0000-0000-000000000005', 'MANTENIMIENTO',       'Mantenimiento',        1, 2, true, false, NULL, true, now()),
                ('33333333-0000-0000-0000-000000000006', 'CONTRATISTA',         'Contratista',          1, 2, true, false, NULL, true, now()),
                ('33333333-0000-0000-0000-000000000007', 'JURIDICO',            'Asesoria juridica',    1, 2, true, false, NULL, true, now()),
                ('33333333-0000-0000-0000-000000000008', 'CONTABLE',            'Asesoria contable',    1, 2, true, false, NULL, true, now()),
                ('33333333-0000-0000-0000-000000000009', 'ASEGURADORA',         'Aseguradora',          1, 2, true, false, NULL, true, now()),
                ('33333333-0000-0000-0000-00000000000a', 'SERVICIOS_PUBLICOS',  'Servicios publicos',   1, 2, true, false, NULL, true, now())
                ON CONFLICT DO NOTHING;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP POLICY IF EXISTS tenant_isolation ON persona_empresas;
                DROP POLICY IF EXISTS tenant_isolation ON directorio_etiquetas;
                DROP POLICY IF EXISTS tenant_isolation ON directorio_contactos;
                DROP POLICY IF EXISTS tenant_isolation ON directorio_vinculos;
                DROP POLICY IF EXISTS tenant_isolation ON etiquetas_catalogo;
            ");

            migrationBuilder.DropForeignKey(
                name: "FK_empresas_personas_representante_legal_persona_id",
                table: "empresas");

            migrationBuilder.DropTable(
                name: "directorio_contactos");

            migrationBuilder.DropTable(
                name: "directorio_etiquetas");

            migrationBuilder.DropTable(
                name: "persona_empresas");

            migrationBuilder.DropTable(
                name: "directorio_vinculos");

            migrationBuilder.DropTable(
                name: "etiquetas_catalogo");

            migrationBuilder.DropIndex(
                name: "IX_empresas_representante_legal_persona_id",
                table: "empresas");

            migrationBuilder.DropColumn(
                name: "acepto_tratamiento_datos",
                table: "personas");

            migrationBuilder.DropColumn(
                name: "canal_aceptacion",
                table: "personas");

            migrationBuilder.DropColumn(
                name: "estado_directorio",
                table: "personas");

            migrationBuilder.DropColumn(
                name: "fecha_aceptacion_datos",
                table: "personas");

            migrationBuilder.DropColumn(
                name: "fecha_nacimiento",
                table: "personas");

            migrationBuilder.DropColumn(
                name: "genero",
                table: "personas");

            migrationBuilder.DropColumn(
                name: "ip_aceptacion",
                table: "personas");

            migrationBuilder.DropColumn(
                name: "perfil_incompleto",
                table: "personas");

            migrationBuilder.DropColumn(
                name: "version_politica_datos",
                table: "personas");

            migrationBuilder.DropColumn(
                name: "estado_directorio",
                table: "empresas");

            migrationBuilder.DropColumn(
                name: "logo_url",
                table: "empresas");

            migrationBuilder.DropColumn(
                name: "perfil_incompleto",
                table: "empresas");

            migrationBuilder.DropColumn(
                name: "regimen_tributario",
                table: "empresas");

            migrationBuilder.DropColumn(
                name: "representante_legal_persona_id",
                table: "empresas");

            migrationBuilder.DropColumn(
                name: "sector_economico",
                table: "empresas");

            migrationBuilder.DropColumn(
                name: "sitio_web",
                table: "empresas");

            migrationBuilder.DropColumn(
                name: "tipo_empresa",
                table: "empresas");
        }
    }
}
