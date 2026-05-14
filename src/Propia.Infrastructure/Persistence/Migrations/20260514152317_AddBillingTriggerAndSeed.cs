using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddBillingTriggerAndSeed : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ---------------------------------------------------------------------
        // Trigger de inmutabilidad para suscripcion_historial.
        // Append-only - solo INSERT permitido. Bloquea UPDATE y DELETE.
        // Mismo patron que super_admin_logs.
        // ---------------------------------------------------------------------
        migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION suscripcion_historial_immutable() RETURNS trigger AS $$
BEGIN
    RAISE EXCEPTION 'suscripcion_historial es append-only - UPDATE y DELETE estan prohibidos';
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_suscripcion_historial_immutable
    BEFORE UPDATE OR DELETE ON suscripcion_historial
    FOR EACH ROW EXECUTE FUNCTION suscripcion_historial_immutable();
");

        // ---------------------------------------------------------------------
        // Seed del singleton billing_config con valores por defecto del spec.
        // Solo se inserta si la tabla esta vacia.
        // ---------------------------------------------------------------------
        migrationBuilder.Sql(@"
INSERT INTO billing_config (
    id, dias_gracia, dia_alerta_mora1, dia_alerta_mora2, dia_suspension,
    dia_alerta_cancelacion, dia_cancelacion, reintentos_cobro,
    dias_preaviso_cobro, retencion_datos_meses, retencion_facturas_anios,
    impuesto_pct, moneda, dias_entre_reintentos, created_at
) VALUES (
    '11111111-2222-3333-4444-555555555555'::uuid,
    5, 6, 10, 15, 30, 45,
    3, 3, 12, 5,
    0, 'COP', '[1,3,7]'::jsonb, now()
)
ON CONFLICT (id) DO NOTHING;
");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DELETE FROM billing_config WHERE id = '11111111-2222-3333-4444-555555555555';");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_suscripcion_historial_immutable ON suscripcion_historial;");
        migrationBuilder.Sql("DROP FUNCTION IF EXISTS suscripcion_historial_immutable();");
    }
}
