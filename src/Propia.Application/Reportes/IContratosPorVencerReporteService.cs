namespace Propia.Application.Reportes;

/// <summary>
/// Reporte "Contratos proximos a vencer" agregando VARIAS copropiedades que el usuario
/// administra (por la conexion multi-tenant). Devuelve datos para graficos + tabla, y un
/// export a Excel de las mismas filas.
/// </summary>
public interface IContratosPorVencerReporteService
{
    Task<ContratosPorVencerReporteDto> GetAsync(IReadOnlyList<Guid>? tenantIds, CancellationToken ct);
    Task<(byte[] Contenido, string NombreArchivo)> ExportarExcelAsync(IReadOnlyList<Guid>? tenantIds, CancellationToken ct);
}
