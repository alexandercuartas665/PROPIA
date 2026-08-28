using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantCodigoCorto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "codigo_corto",
                table: "tenants",
                type: "character varying(6)",
                maxLength: 6,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenants_codigo_corto",
                table: "tenants",
                column: "codigo_corto",
                unique: true,
                filter: "codigo_corto IS NOT NULL");

            // Backfill: asigna un codigo corto unico (6 chars, alfabeto sin ambiguos) a cada
            // copropiedad existente. El loop reintenta hasta encontrar uno libre.
            migrationBuilder.Sql(@"
DO $$
DECLARE
    t RECORD;
    code text;
    alfa text := 'ABCDEFGHJKMNPQRSTUVWXYZ23456789';
    i int;
BEGIN
    FOR t IN SELECT id FROM tenants WHERE codigo_corto IS NULL LOOP
        LOOP
            code := '';
            FOR i IN 1..6 LOOP
                code := code || substr(alfa, 1 + floor(random() * length(alfa))::int, 1);
            END LOOP;
            EXIT WHEN NOT EXISTS (SELECT 1 FROM tenants WHERE codigo_corto = code);
        END LOOP;
        UPDATE tenants SET codigo_corto = code WHERE id = t.id;
    END LOOP;
END $$;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_tenants_codigo_corto",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "codigo_corto",
                table: "tenants");
        }
    }
}
