namespace Propia.Application.Servicios;

/// <summary>
/// Servicios contratados por la copropiedad (modulo Finanzas - Servicios y contratos).
/// Un servicio agrupa uno o mas contratos, tiene un ejecutor y contactos (terceros del
/// Directorio), costos y adjuntos. Los contratos en si se gestionan por IMiCopropiedadService;
/// aqui viven el servicio, sus contactos, sus adjuntos y los adjuntos de contrato.
/// </summary>
public interface IServiciosService
{
    Task<IReadOnlyList<ServicioDto>> ListarAsync(CancellationToken ct);
    Task<ServicioDetalleDto?> GetAsync(Guid id, CancellationToken ct);
    Task<ServicioDto> CrearAsync(CrearServicioRequest req, CancellationToken ct);
    Task<bool> ActualizarAsync(Guid id, ActualizarServicioRequest req, CancellationToken ct);
    Task<bool> EliminarAsync(Guid id, CancellationToken ct);

    Task<ServicioContactoDto> AgregarContactoAsync(Guid servicioId, AgregarServicioContactoRequest req, CancellationToken ct);
    Task<bool> EliminarContactoAsync(Guid contactoId, CancellationToken ct);

    Task<AdjuntoDto> SubirAdjuntoServicioAsync(Guid servicioId, SubirAdjuntoRequest req, Guid? usuarioId, CancellationToken ct);
    Task<bool> EliminarAdjuntoServicioAsync(Guid adjuntoId, CancellationToken ct);

    Task<AdjuntoDto> SubirAdjuntoContratoAsync(Guid contratoId, SubirAdjuntoRequest req, Guid? usuarioId, CancellationToken ct);
    Task<IReadOnlyList<AdjuntoDto>> ListarAdjuntosContratoAsync(Guid contratoId, CancellationToken ct);
    Task<bool> EliminarAdjuntoContratoAsync(Guid adjuntoId, CancellationToken ct);

    /// <summary>Asocia un contrato existente a un servicio.</summary>
    Task<bool> AsociarContratoAsync(Guid servicioId, Guid contratoId, CancellationToken ct);
    /// <summary>Quita el vinculo servicio del contrato.</summary>
    Task<bool> DesasociarContratoAsync(Guid contratoId, CancellationToken ct);

    /// <summary>Agrega una alerta a un servicio (se guarda como AlertaCopropiedad y sale en el dashboard).</summary>
    Task<AlertaServicioDto> AgregarAlertaAsync(Guid servicioId, AgregarAlertaServicioRequest req, CancellationToken ct);
    /// <summary>Resuelve (desactiva) una alerta de servicio.</summary>
    Task<bool> ResolverAlertaAsync(Guid alertaId, CancellationToken ct);
}
