using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EquipoCarga_CoeficientesCincoDecimales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "modulo_contributivo_5",
                table: "unidades_privadas",
                type: "numeric(8,5)",
                precision: 8,
                scale: 5,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(7,4)",
                oldPrecision: 7,
                oldScale: 4,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "modulo_contributivo_4",
                table: "unidades_privadas",
                type: "numeric(8,5)",
                precision: 8,
                scale: 5,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(7,4)",
                oldPrecision: 7,
                oldScale: 4,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "modulo_contributivo_3",
                table: "unidades_privadas",
                type: "numeric(8,5)",
                precision: 8,
                scale: 5,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(7,4)",
                oldPrecision: 7,
                oldScale: 4,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "modulo_contributivo_2",
                table: "unidades_privadas",
                type: "numeric(8,5)",
                precision: 8,
                scale: 5,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(7,4)",
                oldPrecision: 7,
                oldScale: 4,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "modulo_contributivo_1",
                table: "unidades_privadas",
                type: "numeric(8,5)",
                precision: 8,
                scale: 5,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(7,4)",
                oldPrecision: 7,
                oldScale: 4,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "coeficiente_propiedad",
                table: "unidades_privadas",
                type: "numeric(8,5)",
                precision: 8,
                scale: 5,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(7,4)",
                oldPrecision: 7,
                oldScale: 4);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "modulo_contributivo_5",
                table: "unidades_privadas",
                type: "numeric(7,4)",
                precision: 7,
                scale: 4,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(8,5)",
                oldPrecision: 8,
                oldScale: 5,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "modulo_contributivo_4",
                table: "unidades_privadas",
                type: "numeric(7,4)",
                precision: 7,
                scale: 4,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(8,5)",
                oldPrecision: 8,
                oldScale: 5,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "modulo_contributivo_3",
                table: "unidades_privadas",
                type: "numeric(7,4)",
                precision: 7,
                scale: 4,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(8,5)",
                oldPrecision: 8,
                oldScale: 5,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "modulo_contributivo_2",
                table: "unidades_privadas",
                type: "numeric(7,4)",
                precision: 7,
                scale: 4,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(8,5)",
                oldPrecision: 8,
                oldScale: 5,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "modulo_contributivo_1",
                table: "unidades_privadas",
                type: "numeric(7,4)",
                precision: 7,
                scale: 4,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(8,5)",
                oldPrecision: 8,
                oldScale: 5,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "coeficiente_propiedad",
                table: "unidades_privadas",
                type: "numeric(7,4)",
                precision: 7,
                scale: 4,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(8,5)",
                oldPrecision: 8,
                oldScale: 5);
        }
    }
}
