namespace Propia.Application.ServiciosPublicos;

/// <summary>Modulo 2.17 Servicios publicos: cuentas, consumos y reclamaciones.</summary>
public interface IServiciosPublicosService
{
    Task<ServiciosPublicosResumenDto> GetResumenAsync(CancellationToken ct);

    Task<IReadOnlyList<CuentaServicioDto>> ListCuentasAsync(CancellationToken ct);
    Task<CuentaServicioDetalleDto?> GetCuentaAsync(Guid id, CancellationToken ct);
    Task<CuentaServicioDto> CrearCuentaAsync(CrearCuentaServicioRequest req, CancellationToken ct);
    Task<bool> EliminarCuentaAsync(Guid id, CancellationToken ct);

    Task<RegistroConsumoDto> AgregarRegistroAsync(Guid cuentaId, CrearRegistroConsumoRequest req, CancellationToken ct);
    Task<bool> EliminarRegistroAsync(Guid registroId, CancellationToken ct);

    Task<ReclamacionServicioDto> AgregarReclamacionAsync(Guid cuentaId, CrearReclamacionRequest req, CancellationToken ct);
}
