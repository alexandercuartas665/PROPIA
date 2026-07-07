using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEtiquetasUsuario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "etiquetas_usuario",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_etiquetas_usuario", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "usuario_tenant_etiquetas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    etiqueta_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usuario_tenant_etiquetas", x => x.id);
                    table.ForeignKey(
                        name: "FK_usuario_tenant_etiquetas_etiquetas_usuario_etiqueta_id",
                        column: x => x.etiqueta_id,
                        principalTable: "etiquetas_usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_usuario_tenant_etiquetas_usuarios_tenant_usuario_tenant_id",
                        column: x => x.usuario_tenant_id,
                        principalTable: "usuarios_tenant",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_etiquetas_usuario_tenant_id",
                table: "etiquetas_usuario",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_etiquetas_usuario_tenant_id_nombre",
                table: "etiquetas_usuario",
                columns: new[] { "tenant_id", "nombre" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_usuario_tenant_etiquetas_etiqueta_id",
                table: "usuario_tenant_etiquetas",
                column: "etiqueta_id");

            migrationBuilder.CreateIndex(
                name: "IX_usuario_tenant_etiquetas_tenant_id",
                table: "usuario_tenant_etiquetas",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_usuario_tenant_etiquetas_usuario_tenant_id_etiqueta_id",
                table: "usuario_tenant_etiquetas",
                columns: new[] { "usuario_tenant_id", "etiqueta_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "usuario_tenant_etiquetas");

            migrationBuilder.DropTable(
                name: "etiquetas_usuario");
        }
    }
}
