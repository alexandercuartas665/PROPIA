using Propia.Application.MiCopropiedad;

namespace Propia.Application.ServiciosPublicos;

/// <summary>Modulo 2.17 Servicios publicos: cuentas, consumos, recibos leidos con IA y reclamaciones.</summary>
public interface IServiciosPublicosService
{
    Task<ServiciosPublicosResumenDto> GetResumenAsync(CancellationToken ct);

    Task<IReadOnlyList<CuentaServicioDto>> ListCuentasAsync(CancellationToken ct);
    Task<CuentaServicioDetalleDto?> GetCuentaAsync(Guid id, CancellationToken ct);
    Task<CuentaServicioDto> CrearCuentaAsync(CrearCuentaServicioRequest req, CancellationToken ct);
    /// <summary>PUT MERGE. Null si no existe.</summary>
    Task<CuentaServicioDto?> ActualizarCuentaAsync(Guid id, ActualizarCuentaServicioRequest req, CancellationToken ct);
    Task<bool> EliminarCuentaAsync(Guid id, CancellationToken ct);

    Task<RegistroConsumoDto> AgregarRegistroAsync(Guid cuentaId, CrearRegistroConsumoRequest req, CancellationToken ct);
    Task<bool> EliminarRegistroAsync(Guid registroId, CancellationToken ct);

    // ----- Recibos leidos con IA -----
    /// <summary>Valida un recibo leido: cuenta ACTIVA por referencia normalizada, periodo duplicado y topes. No guarda.</summary>
    Task<EvaluacionReciboDto> EvaluarReciboAsync(ReciboLeidoRequest req, CancellationToken ct);
    /// <summary>Guarda el recibo (Origen = IA, lecturas, desglose y archivo); reemplaza el del mismo periodo y alerta si sale de rango.</summary>
    Task<RegistroConsumoDto> ConfirmarReciboAsync(ReciboLeidoRequest req, CancellationToken ct);
    /// <summary>"Es nuestro: agregarlo como servicio": crea la cuenta con los datos del recibo y lo vuelve a evaluar.</summary>
    Task<EvaluacionReciboDto> AgregarServicioDesdeReciboAsync(ReciboLeidoRequest req, CancellationToken ct);

    Task<ReclamacionServicioDto> AgregarReclamacionAsync(Guid cuentaId, CrearReclamacionRequest req, CancellationToken ct);

    // ----- Campos propios (columna "+") -----
    Task<IReadOnlyList<ServicioPublicoCampoDefinicionDto>> ListarCamposAsync(CancellationToken ct);
    Task<ServicioPublicoCampoDefinicionDto> CrearCampoAsync(CrearCampoDefinicionRequest req, CancellationToken ct);
    Task<bool> ActualizarCampoAsync(Guid definicionId, ActualizarCampoDefinicionRequest req, CancellationToken ct);
    Task<bool> EliminarCampoAsync(Guid definicionId, CancellationToken ct);
    Task<IReadOnlyList<ServicioPublicoCampoValorFlatDto>> ListarValoresAsync(CancellationToken ct);
    Task<bool> SetValorAsync(Guid cuentaId, Guid definicionId, string? valor, CancellationToken ct);
}
