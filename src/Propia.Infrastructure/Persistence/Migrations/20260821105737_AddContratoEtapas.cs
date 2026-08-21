using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddContratoEtapas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "etapa_id",
                table: "contratos_servicio",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "contrato_etapas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    color = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contrato_etapas", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_contrato_etapas_tenant_id",
                table: "contrato_etapas",
                column: "tenant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "contrato_etapas");

            migrationBuilder.DropColumn(
                name: "etapa_id",
                table: "contratos_servicio");
        }
    }
}
