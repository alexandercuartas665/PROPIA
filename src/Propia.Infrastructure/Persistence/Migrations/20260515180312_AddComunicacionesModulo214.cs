using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddComunicacionesModulo214 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "comunicado_plantillas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    tipo_comunicado = table.Column<int>(type: "integer", nullable: false),
                    asunto_modelo = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    cuerpo_modelo = table.Column<string>(type: "text", nullable: false),
                    acuse_por_defecto = table.Column<bool>(type: "boolean", nullable: false),
                    es_global = table.Column<bool>(type: "boolean", nullable: false),
                    activa = table.Column<bool>(type: "boolean", nullable: false),
                    creado_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_comunicado_plantillas", x => x.id);
                    table.ForeignKey(
                        name: "FK_comunicado_plantillas_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "comunicados",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    plantilla_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tipo_comunicado = table.Column<int>(type: "integer", nullable: false),
                    asunto = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    cuerpo_html = table.Column<string>(type: "text", nullable: false),
                    cuerpo_texto_plano = table.Column<string>(type: "text", nullable: false),
                    requiere_acuse = table.Column<bool>(type: "boolean", nullable: false),
                    estado = table.Column<int>(type: "integer", nullable: false),
                    fecha_programada = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    fecha_envio = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    fecha_completado = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    total_destinatarios = table.Column<int>(type: "integer", nullable: true),
                    total_entregados = table.Column<int>(type: "integer", nullable: true),
                    total_fallidos = table.Column<int>(type: "integer", nullable: true),
                    archivado_doc_id = table.Column<Guid>(type: "uuid", nullable: true),
                    creado_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cancelado_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cancelado_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_comunicados", x => x.id);
                    table.ForeignKey(
                        name: "FK_comunicados_comunicado_plantillas_plantilla_id",
                        column: x => x.plantilla_id,
                        principalTable: "comunicado_plantillas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "comunicado_adjuntos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    comunicado_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre_archivo = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    tipo_mime = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    tamano_bytes = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("PK_comunicado_adjuntos", x => x.id);
                    table.ForeignKey(
                        name: "FK_comunicado_adjuntos_comunicados_comunicado_id",
                        column: x => x.comunicado_id,
                        principalTable: "comunicados",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "comunicado_destinatarios",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    comunicado_id = table.Column<Guid>(type: "uuid", nullable: false),
                    persona_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token = table.Column<Guid>(type: "uuid", nullable: false),
                    token_expira_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    estado_entrega = table.Column<int>(type: "integer", nullable: false),
                    fecha_entrega = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_comunicado_destinatarios", x => x.id);
                    table.ForeignKey(
                        name: "FK_comunicado_destinatarios_comunicados_comunicado_id",
                        column: x => x.comunicado_id,
                        principalTable: "comunicados",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_comunicado_destinatarios_personas_persona_id",
                        column: x => x.persona_id,
                        principalTable: "personas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "comunicado_segmentos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    comunicado_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_segmento = table.Column<int>(type: "integer", nullable: false),
                    valor_json = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_comunicado_segmentos", x => x.id);
                    table.ForeignKey(
                        name: "FK_comunicado_segmentos_comunicados_comunicado_id",
                        column: x => x.comunicado_id,
                        principalTable: "comunicados",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "comunicado_acuses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    comunicado_id = table.Column<Guid>(type: "uuid", nullable: false),
                    destinatario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    persona_id = table.Column<Guid>(type: "uuid", nullable: false),
                    abierto_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    dispositivo = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_comunicado_acuses", x => x.id);
                    table.ForeignKey(
                        name: "FK_comunicado_acuses_comunicado_destinatarios_destinatario_id",
                        column: x => x.destinatario_id,
                        principalTable: "comunicado_destinatarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_comunicado_acuses_comunicados_comunicado_id",
                        column: x => x.comunicado_id,
                        principalTable: "comunicados",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_comunicado_acuses_comunicado_id_persona_id",
                table: "comunicado_acuses",
                columns: new[] { "comunicado_id", "persona_id" });

            migrationBuilder.CreateIndex(
                name: "IX_comunicado_acuses_destinatario_id",
                table: "comunicado_acuses",
                column: "destinatario_id");

            migrationBuilder.CreateIndex(
                name: "IX_comunicado_acuses_tenant_id",
                table: "comunicado_acuses",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_comunicado_adjuntos_comunicado_id",
                table: "comunicado_adjuntos",
                column: "comunicado_id");

            migrationBuilder.CreateIndex(
                name: "IX_comunicado_adjuntos_tenant_id",
                table: "comunicado_adjuntos",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_comunicado_destinatarios_comunicado_id_persona_id",
                table: "comunicado_destinatarios",
                columns: new[] { "comunicado_id", "persona_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_comunicado_destinatarios_persona_id",
                table: "comunicado_destinatarios",
                column: "persona_id");

            migrationBuilder.CreateIndex(
                name: "IX_comunicado_destinatarios_tenant_id",
                table: "comunicado_destinatarios",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_comunicado_destinatarios_token",
                table: "comunicado_destinatarios",
                column: "token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_comunicado_plantillas_tenant_id",
                table: "comunicado_plantillas",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_comunicado_plantillas_tenant_id_nombre",
                table: "comunicado_plantillas",
                columns: new[] { "tenant_id", "nombre" });

            migrationBuilder.CreateIndex(
                name: "IX_comunicado_segmentos_comunicado_id",
                table: "comunicado_segmentos",
                column: "comunicado_id");

            migrationBuilder.CreateIndex(
                name: "IX_comunicado_segmentos_tenant_id",
                table: "comunicado_segmentos",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_comunicados_plantilla_id",
                table: "comunicados",
                column: "plantilla_id");

            migrationBuilder.CreateIndex(
                name: "IX_comunicados_tenant_id",
                table: "comunicados",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_comunicados_tenant_id_estado",
                table: "comunicados",
                columns: new[] { "tenant_id", "estado" });

            migrationBuilder.CreateIndex(
                name: "IX_comunicados_tenant_id_fecha_programada",
                table: "comunicados",
                columns: new[] { "tenant_id", "fecha_programada" });

            // -----------------------------------------------------------------
            // RLS + GRANTs + triggers (Spec 2.14 - notas para el dev)
            // -----------------------------------------------------------------

            // RLS sobre tablas con tenant_id NOT NULL
            foreach (var tabla in new[] {
                "comunicados",
                "comunicado_segmentos",
                "comunicado_adjuntos",
                "comunicado_destinatarios",
                "comunicado_acuses"
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

            // comunicado_plantillas: tenant_id es NULLABLE (las globales no tienen tenant).
            // Politica: globales (tenant_id IS NULL) visibles para todos; las del tenant
            // solo visibles si coinciden con current_setting('app.tenant_id').
            migrationBuilder.Sql(@"
                ALTER TABLE comunicado_plantillas ENABLE ROW LEVEL SECURITY;
                ALTER TABLE comunicado_plantillas FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_or_global ON comunicado_plantillas
                    USING (tenant_id IS NULL
                           OR tenant_id::text = current_setting('app.tenant_id', true))
                    WITH CHECK (tenant_id IS NULL
                                OR tenant_id::text = current_setting('app.tenant_id', true));
                GRANT SELECT ON comunicado_plantillas TO propia_app;
                GRANT INSERT, UPDATE ON comunicado_plantillas TO propia_app;
                -- DELETE no se otorga: las plantillas globales no se borran (RN-06),
                -- las del tenant se desactivan via flag activa=false.
            ");

            // Trigger inmutabilidad post-envio (RN-04 spec 2.14).
            // Cuando estado pasa a Enviado o Cancelado, los campos de contenido (asunto,
            // cuerpo_html, cuerpo_texto_plano) no pueden modificarse en updates posteriores.
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION comunicados_inmutables_post_envio()
                RETURNS TRIGGER AS $$
                BEGIN
                    IF (OLD.estado IN (4, 5)) THEN  -- 4=Enviado, 5=Cancelado
                        IF (NEW.asunto IS DISTINCT FROM OLD.asunto
                            OR NEW.cuerpo_html IS DISTINCT FROM OLD.cuerpo_html
                            OR NEW.cuerpo_texto_plano IS DISTINCT FROM OLD.cuerpo_texto_plano) THEN
                            RAISE EXCEPTION 'RN-04 spec 2.14: el contenido del comunicado es inmutable una vez Enviado o Cancelado.';
                        END IF;
                    END IF;
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER comunicados_inmutables_check
                    BEFORE UPDATE ON comunicados
                    FOR EACH ROW EXECUTE FUNCTION comunicados_inmutables_post_envio();
            ");

            // Trigger append-only sobre comunicado_acuses (spec 11.1 RN: acuse inmutable).
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION comunicado_acuses_append_only()
                RETURNS TRIGGER AS $$
                BEGIN
                    RAISE EXCEPTION 'comunicado_acuses es append-only (spec 2.14 seccion 11). No se permite % sobre id %.', TG_OP, OLD.id;
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER comunicado_acuses_no_update
                    BEFORE UPDATE ON comunicado_acuses
                    FOR EACH ROW EXECUTE FUNCTION comunicado_acuses_append_only();

                CREATE TRIGGER comunicado_acuses_no_delete
                    BEFORE DELETE ON comunicado_acuses
                    FOR EACH ROW EXECUTE FUNCTION comunicado_acuses_append_only();
            ");

            // Funcion publica SECURITY DEFINER para resolver comunicado por token (sin RLS).
            // El secreto reside en el token (UUID v4) presente solo en el WhatsApp del
            // destinatario. Esta funcion devuelve los datos minimos para servir la vista
            // publica + un endpoint companion registra el acuse via INSERT sobre
            // comunicado_acuses con app.tenant_id seteado al tenant correcto.
            migrationBuilder.Sql(@"
                DROP FUNCTION IF EXISTS get_comunicado_publico(uuid);
                CREATE FUNCTION get_comunicado_publico(p_token uuid)
                RETURNS TABLE (
                    destinatario_id uuid,
                    comunicado_id uuid,
                    tenant_id uuid,
                    persona_id uuid,
                    token_expira_at timestamptz,
                    estado integer,
                    asunto varchar,
                    cuerpo_html text,
                    tipo_comunicado integer,
                    fecha_envio timestamptz,
                    created_at timestamptz,
                    copropiedad_nombre varchar,
                    ya_acusado boolean
                )
                LANGUAGE sql
                SECURITY DEFINER
                SET search_path = public
                AS $$
                    SELECT
                        cd.id, cd.comunicado_id, cd.tenant_id, cd.persona_id,
                        cd.token_expira_at,
                        c.estado, c.asunto, c.cuerpo_html, c.tipo_comunicado,
                        c.fecha_envio, c.created_at,
                        t.nombre,
                        EXISTS(SELECT 1 FROM comunicado_acuses ca WHERE ca.destinatario_id = cd.id)
                    FROM comunicado_destinatarios cd
                    JOIN comunicados c ON c.id = cd.comunicado_id
                    JOIN tenants t ON t.id = c.tenant_id
                    WHERE cd.token = p_token
                    LIMIT 1
                $$;

                GRANT EXECUTE ON FUNCTION get_comunicado_publico(uuid) TO propia_app;
            ");

            // Seed: 7 plantillas globales PropIA (spec seccion 9.2)
            migrationBuilder.Sql(@"
                INSERT INTO comunicado_plantillas
                    (id, tenant_id, nombre, tipo_comunicado, asunto_modelo, cuerpo_modelo, acuse_por_defecto, es_global, activa, created_by, created_at)
                VALUES
                    (gen_random_uuid(), NULL, 'Aviso de corte de agua', 2,
                     'Aviso de corte de agua - {{fecha}}',
                     '<p>Estimados residentes,</p><p>Les informamos que el dia {{fecha}} entre las {{hora_inicio}} y las {{hora_fin}} se realizara un corte programado del suministro de agua por trabajos de mantenimiento. Agradecemos tomar las precauciones necesarias.</p>',
                     false, true, true, NULL, now()),
                    (gen_random_uuid(), NULL, 'Aviso de corte de energia', 2,
                     'Aviso de corte de energia - {{fecha}}',
                     '<p>Estimados residentes,</p><p>Les informamos que el dia {{fecha}} entre las {{hora_inicio}} y las {{hora_fin}} se realizara un corte programado del suministro de energia electrica. Por favor desconecte equipos sensibles antes del corte.</p>',
                     false, true, true, NULL, now()),
                    (gen_random_uuid(), NULL, 'Recordatorio de pago de cuota', 1,
                     'Recordatorio de pago de la cuota de administracion - {{mes}}',
                     '<p>Estimado propietario,</p><p>Le recordamos que la cuota de administracion correspondiente al mes de {{mes}} tiene fecha limite de pago el {{fecha_limite}}. El pago oportuno permite mantener al dia los servicios de la copropiedad.</p>',
                     false, true, true, NULL, now()),
                    (gen_random_uuid(), NULL, 'Citacion informal a reunion', 3,
                     'Invitacion a reunion informal - {{fecha}}',
                     '<p>Estimada comunidad,</p><p>Los invitamos a una reunion informal el dia {{fecha}} a las {{hora}} en {{lugar}}. El tema a tratar es: {{tema}}. Su presencia es importante para la comunidad.</p>',
                     false, true, true, NULL, now()),
                    (gen_random_uuid(), NULL, 'Bienvenida a nuevo residente', 3,
                     'Bienvenidos a {{copropiedad}}',
                     '<p>Estimados nuevos residentes,</p><p>En nombre de la administracion y de toda la comunidad les damos la mas cordial bienvenida a {{copropiedad}}. Para cualquier inquietud sobre el reglamento, servicios o zonas comunes pueden contactar a la administracion.</p>',
                     false, true, true, NULL, now()),
                    (gen_random_uuid(), NULL, 'Comunicado de emergencia', 4,
                     'COMUNICADO URGENTE - {{titulo}}',
                     '<p><strong>ATENCION COMUNIDAD</strong></p><p>{{detalle_emergencia}}</p><p>Por favor seguir las siguientes instrucciones: {{instrucciones}}</p><p>Para mas informacion contactar a la administracion.</p>',
                     true, true, true, NULL, now()),
                    (gen_random_uuid(), NULL, 'Circular de cambio de reglamento', 1,
                     'Modificacion del reglamento de propiedad horizontal',
                     '<p>Estimados propietarios,</p><p>Les informamos formalmente sobre la modificacion del reglamento de propiedad horizontal aprobada en la asamblea del {{fecha_asamblea}}. Los cambios entran en vigor el {{fecha_entrada_vigor}}. Pueden consultar el reglamento actualizado en {{ubicacion_documento}}.</p>',
                     true, true, true, NULL, now());
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Limpieza de triggers y funciones
            migrationBuilder.Sql(@"
                DROP TRIGGER IF EXISTS comunicado_acuses_no_delete ON comunicado_acuses;
                DROP TRIGGER IF EXISTS comunicado_acuses_no_update ON comunicado_acuses;
                DROP FUNCTION IF EXISTS comunicado_acuses_append_only();
                DROP TRIGGER IF EXISTS comunicados_inmutables_check ON comunicados;
                DROP FUNCTION IF EXISTS comunicados_inmutables_post_envio();
                DROP FUNCTION IF EXISTS get_comunicado_publico(uuid);
            ");

            migrationBuilder.DropTable(
                name: "comunicado_acuses");

            migrationBuilder.DropTable(
                name: "comunicado_adjuntos");

            migrationBuilder.DropTable(
                name: "comunicado_segmentos");

            migrationBuilder.DropTable(
                name: "comunicado_destinatarios");

            migrationBuilder.DropTable(
                name: "comunicados");

            migrationBuilder.DropTable(
                name: "comunicado_plantillas");
        }
    }
}
