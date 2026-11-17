using Propia.Domain.Enums;

namespace Propia.Application.Siniestros;

/// <summary>Campo de SISTEMA (fijo) del siniestro. Misma forma canonica que PolizaCampoSistema: el gestor de
/// campos compartido (ConfigCamposEntidad / CrearCampoModal) consume una sola forma por entidad.</summary>
public sealed record SiniestroCampoSistema(
    string Clave,
    string Label,
    TipoCampoTablero Tipo,
    bool VisiblePorDefecto,
    bool Fija = false);

/// <summary>
/// Catalogo de columnas de SISTEMA de /siniestros (entidad de config 'siniestro'). El ORDEN es el orden por
/// defecto de la tabla. Claves en minuscula (unidades-config normaliza con ToLowerInvariant). Las claves de
/// fechas y montos coinciden con las fuentes de Formula del servicio (SiniestrosService.FuentesSistema).
/// </summary>
public static class SiniestroCamposSistema
{
    public static readonly IReadOnlyList<SiniestroCampoSistema> Todos = new[]
    {
        new SiniestroCampoSistema("numero", "N\u00b0 siniestro", TipoCampoTablero.Texto, true, Fija: true),
        new SiniestroCampoSistema("titulo", "T\u00edtulo", TipoCampoTablero.Texto, true),
        new SiniestroCampoSistema("tipo", "Tipo", TipoCampoTablero.Seleccion, true),
        new SiniestroCampoSistema("elemento", "Elemento afectado", TipoCampoTablero.Texto, true),
        new SiniestroCampoSistema("fechaocurrencia", "Ocurrencia", TipoCampoTablero.Fecha, true),
        new SiniestroCampoSistema("estado", "Estado", TipoCampoTablero.Seleccion, true),
        new SiniestroCampoSistema("poliza", "P\u00f3liza", TipoCampoTablero.Texto, true),
        new SiniestroCampoSistema("montoreclamado", "Reclamado", TipoCampoTablero.Moneda, true),
        new SiniestroCampoSistema("montoreconocido", "Reconocido", TipoCampoTablero.Moneda, true),
        // No visibles por defecto:
        new SiniestroCampoSistema("fechareporte", "Reporte", TipoCampoTablero.Fecha, false),
        new SiniestroCampoSistema("numeroreclamacion", "N\u00b0 reclamaci\u00f3n", TipoCampoTablero.Texto, false),
        new SiniestroCampoSistema("responsable", "Responsable", TipoCampoTablero.Texto, false),
        new SiniestroCampoSistema("montoestimado", "Estimado", TipoCampoTablero.Moneda, false),
        new SiniestroCampoSistema("deducible", "Deducible", TipoCampoTablero.Moneda, false),
        new SiniestroCampoSistema("fechapago", "Pago", TipoCampoTablero.Fecha, false),
        new SiniestroCampoSistema("descripcion", "Descripci\u00f3n", TipoCampoTablero.AreaTexto, false),
        new SiniestroCampoSistema("documentos", "Documentos", TipoCampoTablero.Numero, false),
    };

    public static SiniestroCampoSistema? Por(string clave)
        => Todos.FirstOrDefault(c => string.Equals(c.Clave, clave, StringComparison.OrdinalIgnoreCase));
}
