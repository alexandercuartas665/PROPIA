using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGastosPresupuestales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "gastos_presupuestales",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    presupuesto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rubro_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    monto = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    soporte_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    creado_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gastos_presupuestales", x => x.id);
                    table.ForeignKey(
                        name: "FK_gastos_presupuestales_presupuesto_rubros_rubro_id",
                        column: x => x.rubro_id,
                        principalTable: "presupuesto_rubros",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_gastos_presupuestales_presupuestos_presupuesto_id",
                        column: x => x.presupuesto_id,
                        principalTable: "presupuestos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_gastos_presupuestales_presupuesto_id_rubro_id",
                table: "gastos_presupuestales",
                columns: new[] { "presupuesto_id", "rubro_id" });

            migrationBuilder.CreateIndex(
                name: "IX_gastos_presupuestales_rubro_id",
                table: "gastos_presupuestales",
                column: "rubro_id");

            migrationBuilder.CreateIndex(
                name: "IX_gastos_presupuestales_tenant_id",
                table: "gastos_presupuestales",
                column: "tenant_id");

            // RLS por tenant (mismo patron que el resto del modelo).
            migrationBuilder.Sql(@"
                ALTER TABLE gastos_presupuestales ENABLE ROW LEVEL SECURITY;
                ALTER TABLE gastos_presupuestales FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON gastos_presupuestales
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON gastos_presupuestales TO propia_app;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "gastos_presupuestales");
        }
    }
}
