using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddT2Notificaciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "notificaciones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    usuario_destinatario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    persona_destinataria_id = table.Column<Guid>(type: "uuid", nullable: true),
                    canal = table.Column<int>(type: "integer", nullable: false),
                    prioridad = table.Column<int>(type: "integer", nullable: false),
                    estado = table.Column<int>(type: "integer", nullable: false),
                    destino = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    asunto = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    cuerpo = table.Column<string>(type: "text", nullable: false),
                    cuerpo_html = table.Column<string>(type: "text", nullable: true),
                    metadata_json = table.Column<string>(type: "text", nullable: true),
                    modulo_origen_codigo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    entidad_origen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    intentos = table.Column<int>(type: "integer", nullable: false),
                    ultimo_error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    fecha_proximo_intento = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    fecha_enviado = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    fecha_leido = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notificaciones", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_notificaciones_estado_fecha_proximo_intento",
                table: "notificaciones",
                columns: new[] { "estado", "fecha_proximo_intento" });

            migrationBuilder.CreateIndex(
                name: "IX_notificaciones_modulo_origen_codigo_entidad_origen_id",
                table: "notificaciones",
                columns: new[] { "modulo_origen_codigo", "entidad_origen_id" });

            migrationBuilder.CreateIndex(
                name: "IX_notificaciones_tenant_id",
                table: "notificaciones",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_notificaciones_usuario_destinatario_id",
                table: "notificaciones",
                column: "usuario_destinatario_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notificaciones");
        }
    }
}
