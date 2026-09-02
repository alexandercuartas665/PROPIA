namespace Propia.Web;

/// <summary>
/// Conversion de instantes a la hora de COLOMBIA para mostrar en la UI.
///
/// La app es Blazor Server: el renderizado ocurre en el SERVIDOR, asi que <c>DateTime.ToLocalTime()</c>
/// usa la zona horaria del servidor (en produccion = UTC), lo que muestra las fechas +5h para un usuario
/// en Colombia. PROPIA es una plataforma solo-Colombia (Ley 675): Colombia es UTC-5 y NO tiene horario de
/// verano, por lo que un offset fijo de -5 es correcto y portable (Windows/Linux, sin depender de la base
/// de datos de zonas horarias). Usar <c>.ToCo()</c> en vez de <c>.ToLocalTime()</c> al formatear fechas.
/// </summary>
public static class FechaColombia
{
    /// <summary>Offset fijo de Colombia (America/Bogota): UTC-5, sin horario de verano.</summary>
    public static readonly TimeSpan Offset = TimeSpan.FromHours(-5);

    /// <summary>Convierte un instante a la hora de Colombia (para .ToString(...) en la UI).</summary>
    public static DateTimeOffset ToCo(this DateTimeOffset f) => f.ToOffset(Offset);

    /// <summary>
    /// Igual que el anterior para <see cref="DateTime"/>. Un valor Utc/Local se convierte por su instante
    /// real; un valor Unspecified se asume UTC (convencion de almacenamiento de la app).
    /// </summary>
    public static DateTimeOffset ToCo(this DateTime f)
    {
        var utc = f.Kind switch
        {
            DateTimeKind.Utc => f,
            DateTimeKind.Local => f.ToUniversalTime(),
            _ => DateTime.SpecifyKind(f, DateTimeKind.Utc) // Unspecified: la BD guarda UTC
        };
        return new DateTimeOffset(utc, TimeSpan.Zero).ToOffset(Offset);
    }
}
