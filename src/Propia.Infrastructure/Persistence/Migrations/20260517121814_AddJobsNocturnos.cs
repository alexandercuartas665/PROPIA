using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddJobsNocturnos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "job_ejecuciones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    iniciado_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completado_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    estado = table.Column<int>(type: "integer", nullable: false),
                    resultado_json = table.Column<string>(type: "text", nullable: true),
                    error = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ejecutado_por_host = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_ejecuciones", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_job_ejecuciones_job_name_estado",
                table: "job_ejecuciones",
                columns: new[] { "job_name", "estado" });

            migrationBuilder.CreateIndex(
                name: "IX_job_ejecuciones_job_name_iniciado_at",
                table: "job_ejecuciones",
                columns: new[] { "job_name", "iniciado_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "job_ejecuciones");
        }
    }
}
