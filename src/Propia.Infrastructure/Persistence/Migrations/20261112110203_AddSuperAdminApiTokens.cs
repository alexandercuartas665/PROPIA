using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSuperAdminApiTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "super_admin_api_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    prefijo = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    super_admin_usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    expira_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revocado_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ultimo_uso_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ultimo_uso_ip = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_super_admin_api_tokens", x => x.id);
                    table.ForeignKey(
                        name: "FK_super_admin_api_tokens_super_admin_usuarios_super_admin_usu~",
                        column: x => x.super_admin_usuario_id,
                        principalTable: "super_admin_usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_super_admin_api_tokens_super_admin_usuario_id",
                table: "super_admin_api_tokens",
                column: "super_admin_usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_super_admin_api_tokens_token_hash",
                table: "super_admin_api_tokens",
                column: "token_hash",
                unique: true);

            // Tabla global (sin tenant_id, sin RLS): el usuario de runtime la lee y escribe.
            migrationBuilder.Sql("GRANT SELECT, INSERT, UPDATE, DELETE ON super_admin_api_tokens TO propia_app;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "super_admin_api_tokens");
        }
    }
}
