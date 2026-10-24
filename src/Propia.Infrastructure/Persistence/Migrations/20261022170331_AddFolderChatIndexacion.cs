using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFolderChatIndexacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "folder_chat_mensajes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    folder_id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rol = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    texto = table.Column<string>(type: "text", nullable: false),
                    folder_contexto_id = table.Column<Guid>(type: "uuid", nullable: true),
                    input_tokens = table.Column<int>(type: "integer", nullable: false),
                    output_tokens = table.Column<int>(type: "integer", nullable: false),
                    error = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_folder_chat_mensajes", x => x.id);
                    table.ForeignKey(
                        name: "FK_folder_chat_mensajes_folders_folder_id",
                        column: x => x.folder_id,
                        principalTable: "folders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "folder_documento_textos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    documento_id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero_version = table.Column<int>(type: "integer", nullable: false),
                    fuente = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    texto = table.Column<string>(type: "text", nullable: true),
                    caracteres = table.Column<int>(type: "integer", nullable: false),
                    error = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_folder_documento_textos", x => x.id);
                    table.ForeignKey(
                        name: "FK_folder_documento_textos_folder_documentos_documento_id",
                        column: x => x.documento_id,
                        principalTable: "folder_documentos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_folder_chat_mensajes_folder_id",
                table: "folder_chat_mensajes",
                column: "folder_id");

            migrationBuilder.CreateIndex(
                name: "IX_folder_chat_mensajes_tenant_id_folder_id_usuario_id_created~",
                table: "folder_chat_mensajes",
                columns: new[] { "tenant_id", "folder_id", "usuario_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_folder_documento_textos_documento_id",
                table: "folder_documento_textos",
                column: "documento_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_folder_documento_textos_tenant_id",
                table: "folder_documento_textos",
                column: "tenant_id");

            // RLS (aislamiento por tenant) igual que el resto de tablas tenant-scoped.
            foreach (var tabla in new[] { "folder_chat_mensajes", "folder_documento_textos" })
            {
                migrationBuilder.Sql($@"
                    ALTER TABLE {tabla} ENABLE ROW LEVEL SECURITY;
                    ALTER TABLE {tabla} FORCE ROW LEVEL SECURITY;
                    CREATE POLICY tenant_isolation ON {tabla}
                        USING (tenant_id = current_tenant_id())
                        WITH CHECK (tenant_id = current_tenant_id());
                    GRANT SELECT, INSERT, UPDATE, DELETE ON {tabla} TO propia_app;
                ");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "folder_chat_mensajes");

            migrationBuilder.DropTable(
                name: "folder_documento_textos");
        }
    }
}
